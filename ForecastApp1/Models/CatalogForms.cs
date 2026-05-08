using System.ComponentModel.DataAnnotations;

namespace ForecastApp1.Models;

public class SupplierFormModel
{
    public long? Id { get; set; }

    [Display(Name = "Код")]
    [StringLength(64, MinimumLength = 1, ErrorMessage = "Код — до 64 символов")]
    public string? SupplierCode { get; set; }

    [Required(ErrorMessage = "Укажите название")]
    [Display(Name = "Название")]
    [StringLength(500)]
    public string Name { get; set; } = null!;
}

public class ContractFormModel
{
    public long? Id { get; set; }

    [Required(ErrorMessage = "Выберите поставщика")]
    [Display(Name = "Поставщик")]
    public long SupplierId { get; set; }

    [Display(Name = "Внутренний номер")]
    [StringLength(128, MinimumLength = 1, ErrorMessage = "Укажите внутренний номер")]
    public string? InternalContractNumber { get; set; }

    [Display(Name = "Внешний номер")]
    [StringLength(256)]
    public string? ExternalContractNumber { get; set; }

    [Display(Name = "Дата договора")]
    [DataType(DataType.Date)]
    public DateOnly? ContractDate { get; set; }

    [Display(Name = "Сумма договора")]
    public decimal? ContractAmount { get; set; }
}

public class ConditionFormModel
{
    public long? Id { get; set; }

    [Required(ErrorMessage = "Выберите договор")]
    [Display(Name = "Договор")]
    public long ContractId { get; set; }

    [Required]
    [Display(Name = "Действует с")]
    [DataType(DataType.Date)]
    public DateOnly ValidFrom { get; set; }

    [Display(Name = "Действует по")]
    [DataType(DataType.Date)]
    public DateOnly? ValidTo { get; set; }

    [Range(0, 3650, ErrorMessage = "От 0 до 3650 дней")]
    [Display(Name = "Отсрочка, дней")]
    public int PaymentDelayDays { get; set; }

    [Display(Name = "Описание")]
    [StringLength(2000)]
    public string? Description { get; set; }
}
