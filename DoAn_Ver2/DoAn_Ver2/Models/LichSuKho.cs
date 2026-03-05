namespace DoAn_Ver2.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("LichSuKho")]
    public partial class LichSuKho
    {
        public int ID { get; set; }

        public int BienTheSanPhamID { get; set; }

        public int? SoLuongBienDong { get; set; }

        public int? TonThucTeSauBienDong { get; set; }

        [StringLength(255)]
        public string LoaiGiaoDich { get; set; }

        [StringLength(255)]
        public string MaThamChieu { get; set; }

        public DateTime? NgayGhi { get; set; }

        public virtual BienTheSanPham BienTheSanPham { get; set; }
    }
}
