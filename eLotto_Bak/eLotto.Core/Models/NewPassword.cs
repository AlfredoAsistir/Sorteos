using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace eLotto.Core.Models
{
    public class NewPassword
    {
        [StringLength(20)]
        public string User { get; set; }

        [StringLength(10)]
        public string WhatsApp { get; set; }

        [StringLength(128)]
        public string Password { get; set; }

        [StringLength(128)]
        public string ConfirmPassword { get; set; }

        [StringLength(15)]
        public string ConfirmCode { get; set; }
    }
}
