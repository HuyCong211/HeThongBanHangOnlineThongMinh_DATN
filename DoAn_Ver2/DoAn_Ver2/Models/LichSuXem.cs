namespace DoAn_Ver2.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("LichSuXem")]
    public partial class LichSuXem
    {
        public int ID { get; set; }

        public int? NguoiDungID { get; set; }

        public int SanPhamID { get; set; }

        public DateTime? ThoiGian { get; set; }
        [StringLength(255)]
        public string SessionID { get; set; }

        public virtual NguoiDung NguoiDung { get; set; }

        public virtual SanPham SanPham { get; set; }
    }
}
