namespace SecretSanta.ApiService.Database.Model;

public class Wish
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public Guid OwnerId { get; set; }
    public Guid? TargetId { get; set; }
    public string Text { get; set; }

    public virtual Group Group { get; set; }   
    public virtual User Owner { get; set; }
    public virtual User? Target {  get; set; }
}
