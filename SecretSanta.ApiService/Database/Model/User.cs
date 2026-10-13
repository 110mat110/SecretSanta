namespace SecretSanta.ApiService.Database.Model;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; }
    public string Name { get; set; }
    public string GoogleId { get; set; }

    public List<Group> Groups { get; set; }
    public List<Wish> Wishes { get; set; }
}
