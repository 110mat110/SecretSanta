using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecretSanta.ServiceDefaults.Models;
public class ApiWish
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public Guid OwnerId { get; set; }
    public Guid? TargetId { get; set; }
    public string Text { get; set; }
    public string? OwnerName { get; set; }
}
