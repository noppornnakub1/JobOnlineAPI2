using System.ComponentModel.DataAnnotations;

namespace JobOnlineAPI.Models
{
    public class LoginModel
    {
        [Required]
        public required string Username { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public required string Password { get; set; }
    }
}
