namespace AdminTaos.Models;

/// <summary>
/// La fiche partagée d'une personne : ce qu'un collègue a besoin de savoir pour un service.
/// Volontairement pauvre — l'IBAN, l'adresse et l'email restent dans Account, dont la lecture
/// est réservée à l'intéressé et aux admins. N'ajoute rien ici sans mesurer qui pourra le lire.
/// </summary>
public class Profile
{
    public string Id { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? Phone { get; set; }
    public string? PhotoUrl { get; set; }

    public static Profile From(Account a) => new()
    {
        Id = a.Id,
        FullName = a.FullName,
        Phone = a.Phone,
        PhotoUrl = a.PhotoUrl,
    };
}
