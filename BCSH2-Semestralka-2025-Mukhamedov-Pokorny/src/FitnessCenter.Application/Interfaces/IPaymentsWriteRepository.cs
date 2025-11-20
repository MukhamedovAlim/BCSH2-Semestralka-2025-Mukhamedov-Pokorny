public interface IPaymentsWriteRepository
{
    Task<int> CreatePaymentAsync(
        int memberId,
        decimal amount,
        string stavNazev = "Vyřizuje se",
        DateTime? datum = null,
        string? typClenstviNazev = null);

    Task ApproveMembershipPaymentAsync(int idPlatba);

    Task RejectMembershipPaymentAsync(int idPlatba);
}
