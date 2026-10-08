using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace eLotto.Core.Models
{
    public class UserRolsDto
    {
        public int UserId { get; set; }
        public int RolId { get; set; }
        public string RolName { get; set; }
        public DateTime Expire { get; set; }
    }
}
