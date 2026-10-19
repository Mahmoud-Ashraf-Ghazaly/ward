namespace ward.Entities.Enums
{
    public enum ProductType
    {
        FreshFlower = 1,
        ArtificialFlower = 2,
        Bouquet = 3,
        Vase = 4,
        WrappingMaterial = 5,
        Accessory = 6,
        Service = 7
    }

    public enum MovementType
    {
        PurchaseIn = 1,
        SalesOut = 2,
        ReturnIn = 3,
        BouquetAssemblyOut = 4,
        BouquetAssemblyIn = 5,
        StocktakingAdjustment = 6,
        TransferOut = 7,
        TransferIn = 8,
        DamagedDiscardOut = 9,
        PurchaseReturnOut = 10
    }

    public enum PurchaseInvoiceStatus
    {
        Draft = 1,
        Posted = 2,
        PartiallyReturned = 3,
        FullyReturned = 4,
        Cancelled = 5
    }

    public enum PurchaseReturnStatus
    {
        Draft = 1,
        Completed = 2,
        Cancelled = 3
    }

    public enum InvoiceStatus
    {
        Draft = 1,
        Confirmed = 2,
        PartiallyPaid = 3,
        FullyPaid = 4,
        Cancelled = 5,
        PartiallyReturned = 6,
        FullyReturned = 7
    }

    public enum ReturnStatus
    {
        Draft = 1,
        Approved = 2,
        Completed = 3,
        Rejected = 4
    }

    public enum ReturnDisposition
    {
        Restocked = 1,     // Sellable (Restocked into inventory)
        Damaged = 2,       // Damaged (Not restored to inventory)
        Unsellable = 3,    // Unsellable (Not restored to inventory)
        Discarded = 4      // Discarded (Not restored to inventory)
    }

    public enum PaymentMethod
    {
        Cash = 1,
        CreditCard = 2,
        DebitCard = 3,
        BankTransfer = 4,
        GiftCard = 5
    }

    public enum StocktakingStatus
    {
        Draft = 1,
        Counting = 2,
        Reviewed = 3,
        Posted = 4,
        Cancelled = 5
    }
}
