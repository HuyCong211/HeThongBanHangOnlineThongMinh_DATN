namespace DoAn_Ver2.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("GoiYSanPham")]
    public partial class GoiYSanPham
    {
        public int ID { get; set; }

        public int? NguoiDungID { get; set; }

        public int? SanPhamNguonID { get; set; }

        public int SanPhamDuocGoiYID { get; set; }

        public double? DiemGoiY { get; set; }

        [StringLength(250)]
        public string LoaiGoiY { get; set; }

        public DateTime? ThoiDiemTinhToan { get; set; }
        [StringLength(255)]
        public string SessionID { get; set; }

        public virtual NguoiDung NguoiDung { get; set; }

        public virtual SanPham SanPham { get; set; }

        public virtual SanPham SanPham1 { get; set; }
    }
}
