namespace DoAn_Ver2.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("SanPhamYeuThich")]
    public partial class SanPhamYeuThich
    {
        public int ID { get; set; }

        public int NguoiDungID { get; set; }

        public int SanPhamID { get; set; }

        public DateTime? NgayThem { get; set; }

        public virtual NguoiDung NguoiDung { get; set; }

        public virtual SanPham SanPham { get; set; }
    }
}
