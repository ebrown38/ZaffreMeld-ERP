using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZaffreMeld.Web.Models.Shipping;

public class ShipMstr
{
    [Key] public string ShId { get; set; } = string.Empty;
    public string ShCust { get; set; } = string.Empty;
    public string ShShip { get; set; } = string.Empty;
    public int ShPallets { get; set; } = 0;
    public string ShSo { get; set; } = string.Empty;
    public string ShCarrier { get; set; } = string.Empty;
    public string ShTrackno { get; set; } = string.Empty;
    public string ShStatus { get; set; } = "O";
    public string ShSite { get; set; } = string.Empty;
    public string ShEntdate { get; set; } = string.Empty;
    public string ShShipdate { get; set; } = string.Empty;
    [Column(TypeName = "decimal(10,4)")] public decimal ShWeight { get; set; } = 0;
    [Column(TypeName = "decimal(18,4)")] public decimal ShFreightamt { get; set; } = 0;
    public string ShNote { get; set; } = string.Empty;
    public bool ShPosted { get; set; } = false;
    public string ShUser { get; set; } = string.Empty;
}

public class ShipDet
{
    public string ShdId { get; set; } = string.Empty;
    public int ShdLine { get; set; }
    public string ShdItem { get; set; } = string.Empty;
    public string ShdCustitem { get; set; } = string.Empty;
    public string ShdSo { get; set; } = string.Empty;
    public string ShdSoline { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,4)")] public decimal ShdQty { get; set; } = 0;
    public string ShdUom { get; set; } = "EA";
    public string ShdLot { get; set; } = string.Empty;
    public string ShdSerial { get; set; } = string.Empty;
    public string ShdStatus { get; set; } = "O";
    public string ShdNote { get; set; } = string.Empty;
}

public class ShsDet
{
    [Key] public int Id { get; set; }
    public string ShsNbr { get; set; } = string.Empty;
    public string ShsSo { get; set; } = string.Empty;
    public string ShsDesc { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,4)")] public decimal ShsAmt { get; set; } = 0;
    public string ShsType { get; set; } = string.Empty;
    public string ShsAcct { get; set; } = string.Empty;
    public string ShsCc { get; set; } = string.Empty;
}

public class ShipCtrl
{
    [Key] public string ShcSite { get; set; } = string.Empty;
    public string ShcConfirm { get; set; } = "0";
    public string ShcCustitemonly { get; set; } = "0";
    public string ShcAutoInvoice { get; set; } = "0";
}

public class ShipTree
{
    [Key] public int Id { get; set; }
    public string ShipParent { get; set; } = string.Empty;
    public string ShipChild { get; set; } = string.Empty;
    public string ShipSite { get; set; } = string.Empty;
    public string ShipType { get; set; } = string.Empty;
}

public class ShMeta
{
    [Key] public int Id { get; set; }
    public string ShmId { get; set; } = string.Empty;
    public string ShmType { get; set; } = string.Empty;
    public string ShmKey { get; set; } = string.Empty;
    public string ShmValue { get; set; } = string.Empty;
}
