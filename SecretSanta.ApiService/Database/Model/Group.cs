namespace SecretSanta.ApiService.Database.Model;

public class Group
{
    public Guid Id {  get; set; }
    public Guid OwnerId { get; set; }
    public int WishesPerPerson { get; set; }
    public DateTime Deadline { get; set; }
    public bool IsActive { get; set; }
    public string Description { get; set; }
    public string Name { get; set; }

    public virtual User Owner { get; set; }
    public List<Wish> Wishes { get; set; }
}
