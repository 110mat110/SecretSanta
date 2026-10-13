using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SecretSanta.ServiceDefaults.Models;
public class ApiUser
{
    public string? Email { get; set; }
    public string? Name { get; set; }
    public string? GoogleId { get; set; }
}
