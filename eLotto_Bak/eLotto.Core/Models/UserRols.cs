using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace eLotto.Core.Models
{
    public class UserRols
    {
        [Key]
        public int Id { get; set; }
        public int UserId { get; set; }
        public int RolId { get; set; }
        public DateTime Expire { get; set; }
    }
}
