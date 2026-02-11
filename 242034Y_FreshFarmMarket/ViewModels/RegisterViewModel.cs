using System.ComponentModel.DataAnnotations;

namespace _242034Y_FreshFarmMarket.ViewModels
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Full Name is required")]
        [Display(Name = "Full Name")]
        [MaxLength(100, ErrorMessage = "Full Name cannot exceed 100 characters")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Credit Card Number is required")]
        [Display(Name = "Credit Card Number")]
        [RegularExpression(@"^\d{16}$", ErrorMessage = "Credit Card must be 16 digits")]
        [DataType(DataType.CreditCard)]
        public string CreditCardNo { get; set; } = string.Empty;

        [Required(ErrorMessage = "Gender is required")]
        [Display(Name = "Gender")]
        public string Gender { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mobile Number is required")]
        [Display(Name = "Mobile Number")]
        [RegularExpression(@"^[689]\d{7}$",
            ErrorMessage = "Singapore mobile number must be 8 digits starting with 6, 8 or 9")]
        [StringLength(8, MinimumLength = 8, ErrorMessage = "Mobile number must be exactly 8 digits")]
        public string MobileNo { get; set; } = string.Empty;

        [Required(ErrorMessage = "Delivery Address is required")]
        [Display(Name = "Delivery Address")]
        [MaxLength(500, ErrorMessage = "Address cannot exceed 500 characters")]
        public string DeliveryAddress { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        [MinLength(12, ErrorMessage = "Password must be at least 12 characters")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).{12,}$",
            ErrorMessage = "Password must contain uppercase, lowercase, number and special character")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirm Password is required")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Display(Name = "Photo (JPG only)")]
        public IFormFile? Photo { get; set; }

        [Display(Name = "About Me")]
        [MaxLength(2000, ErrorMessage = "About Me cannot exceed 2000 characters")]
        public string? AboutMe { get; set; }
    }
}