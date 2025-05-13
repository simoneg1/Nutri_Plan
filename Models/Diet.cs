using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nutri_Plan.Models
{
    public class Diet
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string UserId { get; set; }

        [ForeignKey("UserId")]
        public User User { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public bool IsActive { get; set; } = true;

        public string Title { get; set; }

        public string Description { get; set; }

        public int TotalCalories { get; set; }

        public double TotalProtein { get; set; }

        public double TotalCarbs { get; set; }

        public double TotalFat { get; set; }

        // Dieta settimanale in formato JSON
        public string DietPlanJson { get; set; }

        // Collection di pasti giornalieri (opzionale)
        public ICollection<DietDay> DietDays { get; set; } = new List<DietDay>();
    }

    public class DietDay
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string DietId { get; set; }

        [ForeignKey("DietId")]
        public Diet Diet { get; set; }

        public int DayNumber { get; set; } // 1 per lunedì, 7 per domenica

        public string DayName { get; set; } // "Lunedì", "Martedì", ecc.

        // Pasti del giorno in formato JSON
        public string MealsJson { get; set; }

        public int DailyCalories { get; set; }

        public double DailyProtein { get; set; }

        public double DailyCarbs { get; set; }

        public double DailyFat { get; set; }
    }
}
