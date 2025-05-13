using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Nutri_Plan.Models
{
    public class User : IdentityUser
    {
        [Required]
        [PersonalData]
        [StringLength(50)]
        public string Nome { get; set; }

        [Required]
        [PersonalData]
        [StringLength(50)]
        public string Cognome { get; set; }

        public bool IsAdmin { get; set; } = false;

        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? LastLoginDate { get; set; }

        // Relazione one-to-one con UserProfile
        public UserProfile Profile { get; set; }

        // Relazione one-to-many con Diet
        public ICollection<Diet> Diets { get; set; }
    }
}