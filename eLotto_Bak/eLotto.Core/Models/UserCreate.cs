using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace eLotto.Core.Models
{
    public class UserCreate
    {
        [Required]
        [StringLength(20, MinimumLength = 8)]
        public string User { get; set; }

        [StringLength(150)]
        public string Name { get; set; }

        [Required]
        [StringLength(128, MinimumLength = 8)]
        public string Password { get; set; }

        [NotMapped]
        [Required]
        [StringLength(128, MinimumLength = 8)]
        public string ConfirmPassword { get; set; }

        [StringLength(15)]
        public string WhatsApp { get; set; }

        public string Device { get; set; }

        public bool ConfirmedOver18 { get; set; }

        public string ReferralCode { get; set; }

    }
}
