namespace ECommerce.Catalog.Domain;

/// <summary>
/// The visibility state of a product. Only <see cref="Active"/> is visible to customers;
/// Hidden and Discontinued are indistinguishable from non-existent (FR-001, FR-002).
/// No transition is triggered by this feature — the authoring path owns them.
/// </summary>
public enum ProductStatus
{
    Draft = 0,
    Active = 1,
    Hidden = 2,
    Discontinued = 3
}
