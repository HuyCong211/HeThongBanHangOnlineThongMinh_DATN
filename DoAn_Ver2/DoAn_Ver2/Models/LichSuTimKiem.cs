namespace DoAn_Ver2.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("LichSuTimKiem")]
    public partial class LichSuTimKiem
    {
        public long ID { get; set; }

        public int? NguoiDungID { get; set; }

        [StringLength(255)]
        public string TuKhoa { get; set; }

        public DateTime? ThoiGian { get; set; }
        [StringLength(255)]
        public string SessionID { get; set; }

        public virtual NguoiDung NguoiDung { get; set; }
    }
}
