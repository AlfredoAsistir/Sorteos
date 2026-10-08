using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using eLotto.Core.Services;

namespace eLotto.Core.Models
{
    public class Users
    {
        [Key]
        public int Id { get; set; }

        [StringLength(150)]
        public string Name { get; set; }

        [StringLength(254)]
        public string Email { get; set; }

        [StringLength(15)]
        public string WhatsApp { get; set; }

        [StringLength(20)]
        public string User { get; set; }

        [StringLength(128)]
        public string Password { get; set; }

        public bool IsActive { get; set; }

        public Guid? ActiveSessionId { get; set; }


        public string ConfirmCode { get; set; }

        public DateTime Date { get; set; }

        public DateTime? Over18ConfirmedAt { get; set; }

        [NotMapped]
        [StringLength(128)]
        public string ConfirmPassword { get; set; }

        public string Languaje { get; set; }

        public bool IsDark { get; set; }
        
        public bool IsAndroid { get; set; }
        
        public bool IsIos { get; set; }

        [StringLength(255)]
        public string StripeCustomerId { get; set; }

        [StringLength(8, MinimumLength = 8)]
        public string ReferralCode { get; set; } = ReferralCodeGenerator.Create();

        public int? ReferredByUserId { get; set; }

        public Users Referrer { get; set; }

        public ICollection<Users> Referrals { get; set; } = new List<Users>();

    }
}
