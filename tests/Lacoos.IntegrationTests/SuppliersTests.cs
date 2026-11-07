using System.Net;
using Lacoos.IntegrationTests.Support;
using Xunit;

namespace Lacoos.IntegrationTests;

public sealed class SuppliersTests(ApiFixture api) : ApiTest(api)
{
    private static async Task<Guid> NewSupplier(TestUser owner, string w, string name = "Foto", string status = "researching") =>
        (await owner.Post($"{w}/suppliers", new { name, category = "Fotografia", negotiationStatus = status })).Json.Id("supplier.id");

    [Fact]
    public async Task Names_are_required()
    {
        var (owner, w, _) = await NewOwner();
        var supplier = await NewSupplier(owner, w);

        var noName = await owner.Post($"{w}/suppliers", new { category = "Local" });
        var serviceNoName = await owner.Post($"{w}/suppliers/{supplier}/services", new { quotedAmount = 10 });

        Assert.Equal("Nome é obrigatório.", noName.Json.Str("errors.name.0"));
        Assert.Equal(HttpStatusCode.BadRequest, serviceNoName.Status);
    }

    [Fact]
    public async Task Supplier_with_budget_items_cannot_be_deleted()
    {
        var (owner, w, _) = await NewOwner();
        var supplier = await NewSupplier(owner, w);
        var category = (await owner.Post($"{w}/budget/categories", new { name = "A" })).Json.Id();
        var item = (await owner.Post($"{w}/budget/items", new { name = "X", categoryId = category, supplierId = supplier })).Json.Id();

        var blocked = await owner.Delete($"{w}/suppliers/{supplier}");
        await owner.Delete($"{w}/budget/items/{item}");
        var allowed = await owner.Delete($"{w}/suppliers/{supplier}");

        Assert.Equal(HttpStatusCode.Conflict, blocked.Status);
        Assert.Equal(HttpStatusCode.NoContent, allowed.Status);
    }

    [Fact]
    public async Task Supplier_with_payments_cannot_be_deleted()
    {
        var (owner, w, _) = await NewOwner();
        var supplier = await NewSupplier(owner, w);
        await owner.Post($"{w}/payments", new { description = "P", totalAmount = 10, paymentMethod = "pix", firstDueDate = "2030-01-01", supplierId = supplier });

        var blocked = await owner.Delete($"{w}/suppliers/{supplier}");

        Assert.Equal(HttpStatusCode.Conflict, blocked.Status);
    }

    [Fact]
    public async Task Financials_ignore_cancelled_services_and_paid_comes_from_payments()
    {
        var (owner, w, _) = await NewOwner();
        var supplier = await NewSupplier(owner, w);
        await owner.Post($"{w}/suppliers/{supplier}/services", new { name = "A", quotedAmount = 1200, contractedAmount = 1000, status = "contracted" });
        await owner.Post($"{w}/suppliers/{supplier}/services", new { name = "B", quotedAmount = 900, contractedAmount = 800, status = "cancelled" });
        var payment = await owner.Post($"{w}/payments", new { description = "P", totalAmount = 1000, paymentMethod = "pix", firstDueDate = "2030-01-01", supplierId = supplier });
        await owner.Post($"{w}/installments/{payment.Json.Id("installments.0.id")}/settlements", new { amount = 250 });

        var detail = await owner.Get($"{w}/suppliers/{supplier}");
        var payments = await owner.Get($"{w}/suppliers/{supplier}/payments");

        Assert.Equal(1200, detail.Json.Dec("supplier.financials.quoted"));
        Assert.Equal(1000, detail.Json.Dec("supplier.financials.contracted"));
        Assert.Equal(250, detail.Json.Dec("supplier.financials.paid"));
        Assert.Equal(750, detail.Json.Dec("supplier.financials.open"));
        Assert.Equal(25, detail.Json.Dec("supplier.financials.paidPercent"));
        Assert.Equal(2, detail.Json.At("services").Count());
        Assert.Equal("partial", payments.Json.Str("0.status"));
    }

    [Fact]
    public async Task List_totals_count_negotiating_statuses()
    {
        var (owner, w, _) = await NewOwner();
        await NewSupplier(owner, w, "A", "contacted");
        await NewSupplier(owner, w, "B", "quoted");
        await NewSupplier(owner, w, "C", "negotiating");
        await NewSupplier(owner, w, "D", "hired");
        await NewSupplier(owner, w, "E", "researching");
        await NewSupplier(owner, w, "F", "cancelled");

        var list = await owner.Get($"{w}/suppliers");

        Assert.Equal(6, list.Json.Int("totals.total"));
        Assert.Equal(1, list.Json.Int("totals.hired"));
        Assert.Equal(3, list.Json.Int("totals.negotiating"));
    }

    [Fact]
    public async Task Search_and_filters()
    {
        var (owner, w, _) = await NewOwner();
        await owner.Post($"{w}/suppliers", new { name = "Luz", category = "Fotografia", contactName = "Marina", email = "m@luz.example.com", phone = "3133330000", negotiationStatus = "hired" });
        await owner.Post($"{w}/suppliers", new { name = "Sabor", category = "Buffet" });

        var byContact = await owner.Get($"{w}/suppliers?search=marina");
        var byPhone = await owner.Get($"{w}/suppliers?search=3333");
        var byCategorySearch = await owner.Get($"{w}/suppliers?search=buff");
        var byCategory = await owner.Get($"{w}/suppliers?category=Buffet");
        var byStatus = await owner.Get($"{w}/suppliers?negotiationStatus=hired");

        Assert.Equal("Luz", byContact.Json.Str("items.0.name"));
        Assert.Equal("Luz", byPhone.Json.Str("items.0.name"));
        Assert.Equal("Sabor", byCategorySearch.Json.Str("items.0.name"));
        Assert.Equal("Sabor", byCategory.Json.Str("items.0.name"));
        Assert.Equal("Luz", byStatus.Json.Str("items.0.name"));
    }

    [Fact]
    public async Task Contract_file_upload_and_download()
    {
        var (owner, w, _) = await NewOwner();
        var supplier = await NewSupplier(owner, w);
        var contract = (await owner.Post($"{w}/suppliers/{supplier}/contracts", new { contractNumber = "2026-1", status = "signed", signedAt = "2026-02-01" })).Json.Id();
        var url = $"{w}/suppliers/{supplier}/contracts/{contract}";

        var noFile = await owner.Get($"{url}/file");
        var upload = await owner.Upload(HttpMethod.Put, $"{url}/file", Scenario.Pdf, "contrato.pdf");
        var download = await owner.Get($"{url}/file");

        Assert.Equal(HttpStatusCode.NotFound, noFile.Status);
        Assert.Equal(HttpStatusCode.OK, upload.Status);
        Assert.Equal("contrato.pdf", upload.Json.Str("file.fileName"));
        Assert.Equal("application/pdf", download.Raw.Content.Headers.ContentType!.MediaType);
        Assert.Equal("attachment", download.Raw.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal("%PDF-1.7 conteudo de teste", download.Text);
    }

    [Fact]
    public async Task Replacing_or_deleting_a_contract_removes_the_stored_file()
    {
        var (owner, w, _) = await NewOwner();
        var supplier = await NewSupplier(owner, w);
        var contract = (await owner.Post($"{w}/suppliers/{supplier}/contracts", new { contractNumber = "1" })).Json.Id();
        var url = $"{w}/suppliers/{supplier}/contracts/{contract}";
        var before = Api.CountUploadedFiles();

        await owner.Upload(HttpMethod.Put, $"{url}/file", Scenario.Pdf, "a.pdf");
        await owner.Upload(HttpMethod.Put, $"{url}/file", Scenario.Png, "b.png");
        var afterReplace = Api.CountUploadedFiles();
        await owner.Delete(url);

        Assert.Equal(before + 1, afterReplace);
        Assert.Equal(before, Api.CountUploadedFiles());
    }

    [Theory]
    [InlineData("virus.exe")]
    [InlineData("pagina.html")]
    [InlineData("sem-extensao")]
    public async Task File_type_is_restricted(string fileName)
    {
        var (owner, w, _) = await NewOwner();
        var supplier = await NewSupplier(owner, w);
        var contract = (await owner.Post($"{w}/suppliers/{supplier}/contracts", new { contractNumber = "1" })).Json.Id();

        var upload = await owner.Upload(HttpMethod.Put, $"{w}/suppliers/{supplier}/contracts/{contract}/file", Scenario.Pdf, fileName);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, upload.Status);
    }

    [Fact]
    public async Task File_content_must_match_its_extension()
    {
        var (owner, w, _) = await NewOwner();
        var supplier = await NewSupplier(owner, w);
        var contract = (await owner.Post($"{w}/suppliers/{supplier}/contracts", new { contractNumber = "1" })).Json.Id();

        var upload = await owner.Upload(HttpMethod.Put, $"{w}/suppliers/{supplier}/contracts/{contract}/file", "<script>alert(1)</script>"u8.ToArray(), "falso.pdf");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, upload.Status);
    }

    [Fact]
    public async Task File_larger_than_the_limit_is_rejected()
    {
        var (owner, w, _) = await NewOwner();
        var supplier = await NewSupplier(owner, w);
        var contract = (await owner.Post($"{w}/suppliers/{supplier}/contracts", new { contractNumber = "1" })).Json.Id();
        var content = new byte[(10 * 1024 * 1024) + 1];
        Scenario.Pdf.CopyTo(content, 0);

        var upload = await owner.Upload(HttpMethod.Put, $"{w}/suppliers/{supplier}/contracts/{contract}/file", content, "grande.pdf");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, upload.Status);
        Assert.Equal("O arquivo deve ter no máximo 10 MB.", upload.Json.Str("detail"));
    }
}
