namespace DoAn_Ver2.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("TinTuc")]
    public partial class TinTuc
    {
        public int ID { get; set; }

        [Required]
        [StringLength(255)]
        public string TieuDe { get; set; }

        [StringLength(255)]
        public string Slug { get; set; }

        [StringLength(500)]
        public string TomTat { get; set; }

        public string NoiDung { get; set; }

        [StringLength(255)]
        public string HinhAnh { get; set; }

        public DateTime? NgayDang { get; set; }

        public int? NguoiDungID { get; set; }

        public bool? TrangThai { get; set; }
        public int? LuotXem { get; set; }
        public virtual NguoiDung NguoiDung { get; set; }
    }
}
