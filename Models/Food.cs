using System.ComponentModel.DataAnnotations;

namespace Nutri_Plan.Models
{
    public class Food
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Nome { get; set; }

        public double Calorie { get; set; } // per 100g

        public double Proteine { get; set; } // in grammi per 100g

        public double Carboidrati { get; set; } // in grammi per 100g

        public double Grassi { get; set; } // in grammi per 100g

        public double Fibre { get; set; } // in grammi per 100g
    }
}