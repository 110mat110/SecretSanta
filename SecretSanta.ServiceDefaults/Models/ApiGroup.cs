using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecretSanta.ServiceDefaults.Models;
public class ApiGroup
{
        public Guid Id { get; set; }
        public Guid OwnerId { get; set; }
        public string Description { get; set; }
        public string Name { get; set; }
        public int WishesPerPerson { get; set; }
        public DateTime Deadline { get; set; }
        public bool IsActive { get; set; }
}
