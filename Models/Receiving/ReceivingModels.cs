using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZaffreMeld.Web.Models.Receiving;

public class RecvMstr
{
    [Key] public string RvId { get; set; } = string.Empty;
    public string RvVend { get; set; } = string.Empty;
    public string RvRecvdate { get; set; } = string.Empty;
    public string RvStatus { get; set; } = "O";
    public string RvSite { get; set; } = string.Empty;
    public string RvNote { get; set; } = string.Empty;
    public string RvUser { get; set; } = string.Empty;
    public bool RvPosted { get; set; } = false;
    public string RvShipvia { get; set; } = string.Empty;
    public string RvTrackno { get; set; } = string.Empty;
    [Column(TypeName = "decimal(10,4)")] public decimal RvWeight { get; set; } = 0;
}

public class RecvDet
{
    public string RvdId { get; set; } = string.Empty;
    public string RvdPo { get; set; } = string.Empty;
    public int RvdPoline { get; set; }
    public string RvdItem { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,4)")] public decimal RvdQty { get; set; } = 0;
    [Column(TypeName = "decimal(18,4)")] public decimal RvdPrice { get; set; } = 0;
    public string RvdUom { get; set; } = "EA";
    public string RvdLot { get; set; } = string.Empty;
    public string RvdSerial { get; set; } = string.Empty;
    public string RvdStatus { get; set; } = "O";
    public string RvdWh { get; set; } = string.Empty;
    public string RvdLoc { get; set; } = string.Empty;
    public string RvdNote { get; set; } = string.Empty;
}
