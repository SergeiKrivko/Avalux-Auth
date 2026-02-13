namespace AvaluxAuth.Abstractions;

public interface ISecretProtector
{
    public string Protect(string plaintext);
    public string Unprotect(string protectedValue);
}