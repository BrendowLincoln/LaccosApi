using Lacoos.Modules.Gifts.Domain;
using Xunit;

namespace Lacoos.UnitTests;

public sealed class GiftRulesTests
{
    private static Gift NewGift(int desired) => new() { Name = "Batedeira", DesiredQuantity = desired };

    private static GiftReservation Reserve(Gift gift, int quantity, ReservationStatus status = ReservationStatus.Active)
    {
        var reservation = new GiftReservation { GiftId = gift.Id, Quantity = quantity, Status = status };
        gift.Reservations.Add(reservation);
        return reservation;
    }

    private static void Receive(Gift gift, int quantity, Guid? reservationId = null) =>
        gift.Receipts.Add(new GiftReceipt { GiftId = gift.Id, Quantity = quantity, ReservationId = reservationId });

    [Fact]
    public void Available_discounts_active_reservations_and_receipts()
    {
        var gift = NewGift(5);
        Reserve(gift, 2);
        Receive(gift, 1);

        Assert.Equal(2, gift.AvailableUnits);
    }

    [Fact]
    public void Available_is_never_negative()
    {
        var gift = NewGift(1);
        Reserve(gift, 3);

        Assert.Equal(0, gift.AvailableUnits);
    }

    [Fact]
    public void Cancelled_reservation_returns_units()
    {
        var gift = NewGift(3);
        Reserve(gift, 2, ReservationStatus.Cancelled);

        Assert.Equal(0, gift.ReservedUnits);
        Assert.Equal(3, gift.AvailableUnits);
    }

    [Fact]
    public void Receipt_from_a_reservation_does_not_count_twice()
    {
        var gift = NewGift(3);
        var reservation = Reserve(gift, 2);
        Receive(gift, 2, reservation.Id);

        Assert.Equal(0, gift.ReservedUnits);
        Assert.Equal(2, gift.ReceivedUnits);
        Assert.Equal(1, gift.AvailableUnits);
    }

    [Fact]
    public void Partial_receipt_keeps_the_balance_of_the_reservation()
    {
        var gift = NewGift(3);
        var reservation = Reserve(gift, 3);
        Receive(gift, 1, reservation.Id);

        Assert.Equal(2, gift.RemainingOf(reservation));
        Assert.Equal(2, gift.ReservedUnits);
        Assert.Equal(1, gift.ReceivedUnits);
        Assert.Equal(0, gift.AvailableUnits);
    }

    [Fact]
    public void Cancelling_a_partly_received_reservation_keeps_the_receipt()
    {
        var gift = NewGift(3);
        var reservation = Reserve(gift, 3);
        Receive(gift, 1, reservation.Id);
        reservation.Status = ReservationStatus.Cancelled;

        Assert.Equal(0, gift.ReservedUnits);
        Assert.Equal(1, gift.ReceivedUnits);
        Assert.Equal(2, gift.AvailableUnits);
    }

    [Fact]
    public void Display_status_is_cancelled_first()
    {
        var gift = NewGift(2);
        Reserve(gift, 1);
        gift.Status = GiftStatus.Cancelled;

        Assert.Equal(GiftDisplayStatus.Cancelled, gift.DisplayStatus);
    }

    [Fact]
    public void Display_status_is_available_while_units_remain()
    {
        var gift = NewGift(3);
        Reserve(gift, 1);
        Receive(gift, 1);

        Assert.Equal(GiftDisplayStatus.Available, gift.DisplayStatus);
    }

    [Fact]
    public void Display_status_is_reserved_when_active_reservations_remain()
    {
        var gift = NewGift(2);
        Reserve(gift, 1);
        Receive(gift, 1);

        Assert.Equal(GiftDisplayStatus.Reserved, gift.DisplayStatus);
    }

    [Fact]
    public void Display_status_is_received_when_everything_arrived()
    {
        var gift = NewGift(2);
        var reservation = Reserve(gift, 2);
        Receive(gift, 2, reservation.Id);

        Assert.Equal(GiftDisplayStatus.Received, gift.DisplayStatus);
    }
}
