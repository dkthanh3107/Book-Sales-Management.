﻿using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Bulky.Models
{
    public class Category
    {
        [Key]
        public int Category_Id { get; set; }
        [Required]
        [DisplayName("Product Name :")]
        public string Name { get; set; }
        [DisplayName("Display Order :")]
        [Range(1,100 , ErrorMessage = "Display Order must be between 1-100")]
        public int DisplayOrder { get; set; }
    }
}

