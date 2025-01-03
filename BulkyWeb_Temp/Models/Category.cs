﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel;

namespace BulkyWeb_Temp.Models
{
        public class Category
        {
            [Key]
            public int Category_Id { get; set; }
            [Required]
            [DisplayName("Category Name :")]
            public string Name { get; set; }
            [DisplayName("Display Order :")]
            [Range(1, 100, ErrorMessage = "Display Order must be between 1-100")]
            public int DisplayOrder { get; set; }
        }
}
