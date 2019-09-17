using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WeiboFav.Model
{
    [Table("Img")]
    public class Img
    {
        [Key] public int ImgId { get; set; }

        [Required] public string ImgUrl { get; set; }

        public string ImgPath { get; set; }
    }
}