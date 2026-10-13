namespace Pacus.Domain.Entities;

/// <summary>
/// Configuração de hidratação de um perfil. É uma meta configurável do aplicativo,
/// não uma recomendação clínica automática.
/// </summary>
public class WaterProfileSettings
{
    public int GoalMl { get; set; } = 2000;
    public int RewardPoints { get; set; } = 5;
}
