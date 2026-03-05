namespace DoAn_Ver2.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("GioHangChiTiet")]
    public partial class GioHangChiTiet
    {
        public int ID { get; set; }

        public int GioHangID { get; set; }

        public int BienTheSanPhamID { get; set; }

        public int? SoLuong { get; set; }

        public virtual BienTheSanPham BienTheSanPham { get; set; }

        public virtual GioHang GioHang { get; set; }
    }
}
