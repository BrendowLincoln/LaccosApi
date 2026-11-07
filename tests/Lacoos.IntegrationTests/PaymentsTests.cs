using System.Net;
using Lacoos.IntegrationTests.Support;
using Xunit;

namespace Lacoos.IntegrationTests;

public sealed class PaymentsTests(ApiFixture api) : ApiTest(api)
{
    private static Task<ApiResponse> NewPayment(TestUser owner, string w, decimal total, int count = 1, string firstDueDate = "2030-01-31", string description = "Pagamento") =>
        owner.Post($"{w}/payments", new { description, totalAmount = total, paymentMethod = "pix", installmentCount = count, firstDueDate });

    [Fact]
    public async Task Description_is_required_and_total_cannot_be_negative()
    {
        var (owner, w, _) = await NewOwner();

        var noDescription = await owner.Post($"{w}/payments", new { totalAmount = 10, paymentMethod = "pix", firstDueDate = "2030-01-01" });
        var negative = await NewPayment(owner, w, -1);

        Assert.Equal("Descrição é obrigatório.", noDescription.Json.Str("errors.description.0"));
        Assert.Equal("Valor total não pode ser negativo.", negative.Json.Str("errors.totalAmount.0"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    public async Task Installment_count_must_be_between_1_and_24(int count)
    {
        var (owner, w, _) = await NewOwner();

        var response = await NewPayment(owner, w, 100, count);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
    }

    [Fact]
    public async Task Generates_installments_with_remainder_and_monthly_due_dates()
    {
        var (owner, w, _) = await NewOwner();

        var payment = await NewPayment(owner, w, 100, 3, "2031-01-31");

        Assert.Equal(33.33m, payment.Json.Dec("installments.0.amount"));
        Assert.Equal(33.33m, payment.Json.Dec("installments.1.amount"));
        Assert.Equal(33.34m, payment.Json.Dec("installments.2.amount"));
        Assert.Equal("2031-01-31", payment.Json.Str("installments.0.dueDate"));
        Assert.Equal("2031-02-28", payment.Json.Str("installments.1.dueDate"));
        Assert.Equal("2031-03-31", payment.Json.Str("installments.2.dueDate"));
    }

    [Fact]
    public async Task Custom_due_dates_can_be_sent_and_adjusted_later()
    {
        var (owner, w, _) = await NewOwner();

        var payment = await owner.Post($"{w}/payments", new { description = "P", totalAmount = 100, paymentMethod = "boleto", dueDates = new[] { "2030-01-05", "2030-03-07" } });
        var wrongCount = await owner.Post($"{w}/payments", new { description = "P", totalAmount = 100, paymentMethod = "boleto", installmentCount = 3, dueDates = new[] { "2030-01-05" } });
        var adjusted = await owner.Patch($"{w}/payments/{payment.Json.Id()}/installments/2", new { dueDate = "2030-04-01" });

        Assert.Equal(2, payment.Json.At("installments").Count());
        Assert.Equal("2030-03-07", payment.Json.Str("installments.1.dueDate"));
        Assert.Equal(HttpStatusCode.BadRequest, wrongCount.Status);
        Assert.Equal("2030-04-01", adjusted.Json.Str("installments.1.dueDate"));
    }

    [Fact]
    public async Task Changing_the_total_without_settlements_recalculates_and_keeps_due_dates()
    {
        var (owner, w, _) = await NewOwner();
        var payment = await NewPayment(owner, w, 100, 3);
        var id = payment.Json.Id();
        await owner.Patch($"{w}/payments/{id}/installments/2", new { dueDate = "2030-06-15" });

        var updated = await owner.Put($"{w}/payments/{id}", new { description = "Novo", totalAmount = 200, paymentMethod = "cash" });

        Assert.Equal(200, updated.Json.Dec("totalAmount"));
        Assert.Equal(66.66m, updated.Json.Dec("installments.0.amount"));
        Assert.Equal(66.68m, updated.Json.Dec("installments.2.amount"));
        Assert.Equal("2030-06-15", updated.Json.Str("installments.1.dueDate"));
        Assert.Equal(200, updated.Json.At("installments").EnumerateArray().Sum(i => i.Dec("amount")));
    }

    [Fact]
    public async Task Changing_the_installment_count_without_settlements_regenerates_them()
    {
        var (owner, w, _) = await NewOwner();
        var payment = await NewPayment(owner, w, 100, 2);

        var updated = await owner.Put($"{w}/payments/{payment.Json.Id()}", new { description = "P", totalAmount = 100, paymentMethod = "pix", installmentCount = 4, firstDueDate = "2031-01-10" });

        Assert.Equal(HttpStatusCode.OK, updated.Status);
        Assert.Equal(4, updated.Json.At("installments").Count());
        Assert.Equal("2031-04-10", updated.Json.Str("installments.3.dueDate"));
        Assert.Equal(100, updated.Json.At("installments").EnumerateArray().Sum(i => i.Dec("amount")));
    }

    [Fact]
    public async Task Changing_the_total_with_settlements_is_rejected_but_other_fields_can_change()
    {
        var (owner, w, _) = await NewOwner();
        var payment = await NewPayment(owner, w, 100, 2);
        var id = payment.Json.Id();
        await owner.Post($"{w}/installments/{payment.Json.Id("installments.0.id")}/settlements", new { amount = 10 });

        var total = await owner.Put($"{w}/payments/{id}", new { description = "P", totalAmount = 200, paymentMethod = "pix" });
        var count = await owner.Put($"{w}/payments/{id}", new { description = "P", totalAmount = 100, paymentMethod = "pix", installmentCount = 5 });
        var description = await owner.Put($"{w}/payments/{id}", new { description = "Só a descrição", totalAmount = 100, paymentMethod = "pix" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, total.Status);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, count.Status);
        Assert.Equal("Só a descrição", description.Json.Str("description"));
        Assert.Equal(10, description.Json.Dec("paidAmount"));
    }

    [Fact]
    public async Task Settlements_accumulate_and_excess_is_rejected_without_truncating()
    {
        var (owner, w, _) = await NewOwner();
        var payment = await NewPayment(owner, w, 100);
        var installment = payment.Json.Id("installments.0.id");
        var url = $"{w}/installments/{installment}/settlements";

        var first = await owner.Post(url, new { amount = 60, paymentMethod = "cash", paidAt = "2030-01-10", notes = "Sinal" });
        var excess = await owner.Post(url, new { amount = 40.01 });
        var partial = await owner.Get($"{w}/payments/{payment.Json.Id()}");
        var second = await owner.Post(url, new { amount = 40 });
        var more = await owner.Post(url, new { amount = 0.01 });
        var paid = await owner.Get($"{w}/payments/{payment.Json.Id()}");

        Assert.Equal(HttpStatusCode.Created, first.Status);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, excess.Status);
        Assert.Equal(60, partial.Json.Dec("installments.0.paidAmount"));
        Assert.Equal(40, partial.Json.Dec("installments.0.remainingAmount"));
        Assert.Equal("partial", partial.Json.Str("installments.0.status"));
        Assert.Equal("cash", partial.Json.Str("installments.0.paymentMethod"));
        Assert.Equal("2030-01-10", partial.Json.Str("installments.0.paidAt"));
        Assert.Equal("Sinal", partial.Json.Str("installments.0.notes"));
        Assert.Equal(HttpStatusCode.Created, second.Status);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, more.Status);
        Assert.Equal(100, paid.Json.Dec("paidAmount"));
        Assert.Equal(0, paid.Json.Dec("remainingAmount"));
        Assert.Equal("paid", paid.Json.Str("status"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Settlement_must_be_positive(decimal amount)
    {
        var (owner, w, _) = await NewOwner();
        var payment = await NewPayment(owner, w, 100);

        var response = await owner.Post($"{w}/installments/{payment.Json.Id("installments.0.id")}/settlements", new { amount });

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
    }

    [Fact]
    public async Task Concurrent_settlements_cannot_exceed_the_installment()
    {
        var (owner, w, _) = await NewOwner();
        var payment = await NewPayment(owner, w, 100);
        var url = $"{w}/installments/{payment.Json.Id("installments.0.id")}/settlements";

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => owner.Post(url, new { amount = 60 })));
        var reloaded = await owner.Get($"{w}/payments/{payment.Json.Id()}");

        Assert.Single(responses, r => r.Status == HttpStatusCode.Created);
        Assert.Equal(60, reloaded.Json.Dec("paidAmount"));
    }

    [Fact]
    public async Task Payment_with_settlements_cannot_be_deleted_even_after_reversal()
    {
        var (owner, w, _) = await NewOwner();
        var payment = await NewPayment(owner, w, 100);
        var installment = payment.Json.Id("installments.0.id");
        await owner.Post($"{w}/installments/{installment}/settlements", new { amount = 100 });

        var blocked = await owner.Delete($"{w}/payments/{payment.Json.Id()}");
        await owner.Post($"{w}/installments/{installment}/reversal", new { confirm = true, reason = "Erro" });
        var stillBlocked = await owner.Delete($"{w}/payments/{payment.Json.Id()}");

        Assert.Equal(HttpStatusCode.Conflict, blocked.Status);
        Assert.Equal(HttpStatusCode.Conflict, stillBlocked.Status);
    }

    [Fact]
    public async Task Payment_without_settlements_can_be_deleted()
    {
        var (owner, w, _) = await NewOwner();
        var payment = await NewPayment(owner, w, 100);

        var deleted = await owner.Delete($"{w}/payments/{payment.Json.Id()}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.Status);
    }

    [Fact]
    public async Task Reversal_requires_explicit_confirmation_and_a_reason()
    {
        var (owner, w, _) = await NewOwner();
        var payment = await NewPayment(owner, w, 100);
        var installment = payment.Json.Id("installments.0.id");
        await owner.Post($"{w}/installments/{installment}/settlements", new { amount = 100 });

        var noConfirm = await owner.Post($"{w}/installments/{installment}/reversal", new { reason = "Erro" });
        var noReason = await owner.Post($"{w}/installments/{installment}/reversal", new { confirm = true });

        Assert.Equal(HttpStatusCode.BadRequest, noConfirm.Status);
        Assert.Equal(HttpStatusCode.BadRequest, noReason.Status);
        Assert.Equal(100, (await owner.Get($"{w}/payments/{payment.Json.Id()}")).Json.Dec("paidAmount"));
    }

    [Fact]
    public async Task Reversal_keeps_the_history_and_reopens_the_installment()
    {
        var (owner, w, _) = await NewOwner();
        var payment = await NewPayment(owner, w, 100);
        var installment = payment.Json.Id("installments.0.id");
        await owner.Post($"{w}/installments/{installment}/settlements", new { amount = 30 });
        await owner.Post($"{w}/installments/{installment}/settlements", new { amount = 70 });

        var reversal = await owner.Post($"{w}/installments/{installment}/reversal", new { confirm = true, reason = "Cobrança duplicada" });
        var history = await owner.Get($"{w}/installments/{installment}/settlements");
        var reloaded = await owner.Get($"{w}/payments/{payment.Json.Id()}");
        var again = await owner.Post($"{w}/installments/{installment}/reversal", new { confirm = true, reason = "De novo" });
        var settleAgain = await owner.Post($"{w}/installments/{installment}/settlements", new { amount = 100 });

        Assert.Equal(100, reversal.Json.Dec("amount"));
        Assert.Equal(3, history.Json.Count());
        Assert.Equal("settlement", history.Json.Str("0.type"));
        Assert.Equal("reversal", history.Json.Str("2.type"));
        Assert.Equal("Cobrança duplicada", history.Json.Str("2.notes"));
        Assert.Equal(owner.Id, history.Json.Id("2.createdByUserId"));
        Assert.Equal(0, reloaded.Json.Dec("paidAmount"));
        Assert.Equal("pending", reloaded.Json.Str("installments.0.status"));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, reloaded.Json.At("installments.0.paidAt").ValueKind);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, again.Status);
        Assert.Equal(HttpStatusCode.Created, settleAgain.Status);
    }

    [Fact]
    public async Task Status_is_recalculated_as_time_passes()
    {
        var (owner, w, _) = await NewOwner();
        Api.Clock.SetToday(new DateOnly(2030, 1, 10));
        var payment = await NewPayment(owner, w, 100, 1, "2030-01-10");
        var url = $"{w}/payments/{payment.Json.Id()}";

        var onDueDate = await owner.Get(url);
        Api.Clock.SetToday(new DateOnly(2030, 1, 11));
        var dayAfter = await owner.Get(url);

        Assert.Equal("pending", onDueDate.Json.Str("installments.0.status"));
        Assert.Equal("overdue", dayAfter.Json.Str("installments.0.status"));
        Assert.Equal("overdue", dayAfter.Json.Str("status"));
    }

    [Fact]
    public async Task Overdue_uses_sao_paulo_time_and_not_utc()
    {
        var (owner, w, _) = await NewOwner();
        var payment = await NewPayment(owner, w, 100, 1, "2030-03-10");
        var url = $"{w}/payments/{payment.Json.Id()}";

        // 02:00 UTC do dia 11 ainda é 23:00 do dia 10 em São Paulo.
        Api.Clock.FixedAt = new DateTimeOffset(2030, 3, 11, 2, 0, 0, TimeSpan.Zero);
        var lateEvening = await owner.Get(url);
        Api.Clock.FixedAt = new DateTimeOffset(2030, 3, 11, 3, 0, 0, TimeSpan.Zero);
        var midnight = await owner.Get(url);

        Assert.Equal("pending", lateEvening.Json.Str("status"));
        Assert.Equal("overdue", midnight.Json.Str("status"));
    }

    [Fact]
    public async Task Paid_installment_is_never_overdue_and_partial_overdue_keeps_the_order()
    {
        var (owner, w, _) = await NewOwner();
        Api.Clock.SetToday(new DateOnly(2030, 1, 1));
        var payment = await NewPayment(owner, w, 200, 2, "2030-01-10");
        await owner.Post($"{w}/installments/{payment.Json.Id("installments.0.id")}/settlements", new { amount = 100 });
        await owner.Post($"{w}/installments/{payment.Json.Id("installments.1.id")}/settlements", new { amount = 10 });

        Api.Clock.SetToday(new DateOnly(2030, 6, 1));
        var reloaded = await owner.Get($"{w}/payments/{payment.Json.Id()}");

        Assert.Equal("paid", reloaded.Json.Str("installments.0.status"));
        Assert.Equal("overdue", reloaded.Json.Str("installments.1.status"));
        Assert.Equal("overdue", reloaded.Json.Str("status"));
    }

    [Fact]
    public async Task List_searches_filters_views_and_totals()
    {
        var (owner, w, _) = await NewOwner();
        Api.Clock.SetToday(new DateOnly(2030, 6, 1));
        var supplier = (await owner.Post($"{w}/suppliers", new { name = "Luz Fotografia", category = "Fotografia" })).Json.Id("supplier.id");
        var category = (await owner.Post($"{w}/budget/categories", new { name = "A" })).Json.Id();
        var item = (await owner.Post($"{w}/budget/items", new { name = "Decoração floral", categoryId = category })).Json.Id();

        var overdue = await owner.Post($"{w}/payments", new { description = "Atrasado", totalAmount = 300, paymentMethod = "pix", firstDueDate = "2030-05-01", supplierId = supplier });
        await owner.Post($"{w}/installments/{overdue.Json.Id("installments.0.id")}/settlements", new { amount = 100 });
        var paid = await owner.Post($"{w}/payments", new { description = "Quitado", totalAmount = 200, paymentMethod = "pix", firstDueDate = "2030-05-01" });
        await owner.Post($"{w}/installments/{paid.Json.Id("installments.0.id")}/settlements", new { amount = 200 });
        await owner.Post($"{w}/payments", new { description = "Futuro", totalAmount = 500, paymentMethod = "pix", firstDueDate = "2030-07-01", budgetItemId = item });

        var all = await owner.Get($"{w}/payments");
        var bySupplierName = await owner.Get($"{w}/payments?search=luz");
        var byItemName = await owner.Get($"{w}/payments?search=floral");
        var byDescription = await owner.Get($"{w}/payments?search=quit");
        var bySupplier = await owner.Get($"{w}/payments?supplierId={supplier}");
        var byStatus = await owner.Get($"{w}/payments?status=overdue");
        var pending = await owner.Get($"{w}/payments?view=pending");
        var overdueView = await owner.Get($"{w}/payments?view=overdue");
        var done = await owner.Get($"{w}/payments?view=paid");

        Assert.Equal(1000, all.Json.Dec("totals.total"));
        Assert.Equal(300, all.Json.Dec("totals.paid"));
        Assert.Equal(700, all.Json.Dec("totals.open"));
        Assert.Equal(200, all.Json.Dec("totals.overdueAmount"));
        Assert.Equal(1, all.Json.Int("totals.overdueCount"));
        Assert.Equal(30, all.Json.Dec("totals.paidPercent"));
        Assert.Equal("Atrasado", bySupplierName.Json.Str("items.0.description"));
        Assert.Equal("Luz Fotografia", bySupplierName.Json.Str("items.0.supplierName"));
        Assert.Equal("Futuro", byItemName.Json.Str("items.0.description"));
        Assert.Equal("Decoração floral", byItemName.Json.Str("items.0.budgetItemName"));
        Assert.Equal("Quitado", byDescription.Json.Str("items.0.description"));
        Assert.Equal(1, bySupplier.Json.At("items").Count());
        Assert.Equal("Atrasado", byStatus.Json.Str("items.0.description"));
        Assert.Equal("Futuro", pending.Json.Str("items.0.description"));
        Assert.Equal("Atrasado", overdueView.Json.Str("items.0.description"));
        Assert.Equal("Quitado", done.Json.Str("items.0.description"));
    }

    [Fact]
    public async Task Several_receipts_can_be_attached_downloaded_and_deleted()
    {
        var (owner, w, _) = await NewOwner();
        var payment = await NewPayment(owner, w, 100);
        var installment = payment.Json.Id("installments.0.id");
        var settlement = await owner.Post($"{w}/installments/{installment}/settlements", new { amount = 100 });

        var first = await owner.Upload(HttpMethod.Post, $"{w}/installments/{installment}/receipts", Scenario.Pdf, "pix.pdf",
            fields: new Dictionary<string, string> { ["transactionId"] = settlement.Json.Id().ToString() });
        var second = await owner.Upload(HttpMethod.Post, $"{w}/installments/{installment}/receipts", Scenario.Png, "foto.png");
        var reloaded = await owner.Get($"{w}/payments/{payment.Json.Id()}");
        var download = await owner.Get($"{w}/receipts/{first.Json.Id()}");
        var deleted = await owner.Delete($"{w}/receipts/{second.Json.Id()}");
        var afterDelete = await owner.Get($"{w}/receipts/{second.Json.Id()}");

        Assert.Equal(HttpStatusCode.Created, first.Status);
        Assert.Equal(settlement.Json.Id(), first.Json.Id("transactionId"));
        Assert.Equal(2, reloaded.Json.At("installments.0.receipts").Count());
        Assert.Equal("pix.pdf", reloaded.Json.Str("installments.0.receipts.0.fileName"));
        Assert.Equal("%PDF-1.7 conteudo de teste", download.Text);
        Assert.Equal(HttpStatusCode.NoContent, deleted.Status);
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.Status);
    }

    [Fact]
    public async Task Receipt_cannot_point_to_a_settlement_of_another_installment()
    {
        var (owner, w, _) = await NewOwner();
        var payment = await NewPayment(owner, w, 100, 2);
        var settlement = await owner.Post($"{w}/installments/{payment.Json.Id("installments.0.id")}/settlements", new { amount = 10 });

        var response = await owner.Upload(HttpMethod.Post, $"{w}/installments/{payment.Json.Id("installments.1.id")}/receipts", Scenario.Pdf, "a.pdf",
            fields: new Dictionary<string, string> { ["transactionId"] = settlement.Json.Id().ToString() });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.Status);
    }
}
