namespace AegisDrive.Api.Entities;

public abstract class BaseEntity<TKey> 
{
    public TKey Id { get; set; }
    
    public DateTime CreatedOnUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedOnUtc { get; set; } 
    
    public bool IsDeleted { get; set; } = false;

}