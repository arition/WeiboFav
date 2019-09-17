using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WeiboFav.Model
{
    [Table("WeiboInfo")]
    public class WeiboInfo
    {
        [Key] public int WeiboInfoId { get; set; }

        [Required] public string Id { get; set; }

        [Required] public string RawHtml { get; set; }

        public List<Img> ImgUrls { get; set; }

        public string VideoUrl { get; set; }

        public string Url { get; set; }
    }
}