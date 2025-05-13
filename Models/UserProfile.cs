using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nutri_Plan.Models
{
    public class UserProfile
    {
        [Key]
        public string Id { get; set; }

        [Required]
        public int Eta { get; set; }

        [Required]
        [Range(30, 300)]
        public double Peso { get; set; } // in kg

        [Required]
        [Range(100, 250)]
        public double Altezza { get; set; } // in cm

        [Required]
        public string LivelloAttivita { get; set; } // Sedentario, Moderato, Attivo, Molto Attivo

        [Required]
        public string ObbiettivoPeso { get; set; } // Dimagrire, Mantenere, Ingrassare

        public string CiboPreferito { get; set; }

        // Relazione one-to-one con User
        public string UserId { get; set; }

        [ForeignKey("UserId")]
        public User User { get; set; }
    }
}