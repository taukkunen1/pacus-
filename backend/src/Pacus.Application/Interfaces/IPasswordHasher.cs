namespace Pacus.Application.Interfaces;

// Usado tanto para a senha do adulto quanto para o PIN da crianca — mesmo mecanismo,
// campos separados no banco (User.PasswordHash / User.PinHash).
public interface IPasswordHasher
{
    string Hash(string plainText);
    bool Verify(string hash, string plainText);
}
