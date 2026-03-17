using Avalux.Auth.ApiClient.Models;

namespace Avalux.Auth.ApiClient;

public interface IAuthClient
{
    /// <summary>
    /// Получение всех пользователей, зарегистрированных в приложении. Обязательно разделение на страницы.
    /// Требует разрешение <c>readUserInfo</c>.
    /// </summary>
    /// <param name="page">Номер страницы</param>
    /// <param name="limit">Количество записей на одной странице</param>
    /// <param name="ct">CancellationToken</param>
    /// <returns></returns>
    public Task<IEnumerable<UserInfo>> GetUsersAsync(int page = 0, int limit = 100, CancellationToken ct = default);

    /// <summary>
    /// Поиск пользователей по логину или почте. Также включает фильтрацию по провайдерам.
    /// Возвращает пользователей и аккаунты, удовлетворяющие фильтрам.
    /// Требует разрешение <c>readUserInfo</c>.
    /// </summary>
    /// <param name="page">Номер страницы</param>
    /// <param name="limit">Количество записей на одной странице. Необязательный параметр</param>
    /// <param name="login">Логин пользователя для поиска. Необязательный параметр</param>
    /// <param name="email">Почта пользователя для поиска. Необязательный параметр</param>
    /// <param name="provider">Строковый идентификатор провайдера. Необязательный параметр</param>
    /// <param name="ct">CancellationToken</param>
    /// <returns>CancellationToken</returns>
    public Task<IEnumerable<UserInfo>> SearchUsersAsync(int page = 0, int limit = 100,
        string? login = null, string? email = null, string? provider = null,
        CancellationToken ct = default);

    /// <summary>
    /// Получение информации о пользователе по его ID.
    /// Требует разрешение <c>readUserInfo</c>.
    /// </summary>
    /// <param name="id">ID пользователя</param>
    /// <param name="ct">CancellationToken</param>
    /// <returns></returns>
    public Task<UserInfo> GetUserAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Получение токена доступа провайдера.
    /// Требует разрешение <c>readUserAccessToken</c>.
    /// </summary>
    /// <param name="id">ID пользователя</param>
    /// <param name="provider">Строковый идентификатор провайдера</param>
    /// <param name="ct">CancellationToken</param>
    /// <returns></returns>
    public Task<AccountCredentials> GetAccessTokenAsync(Guid id, string provider, CancellationToken ct = default);

    /// <summary>
    /// Удаление пользователя.
    /// Требует разрешение <c>deleteUser</c>.
    /// </summary>
    /// <param name="id">ID пользователя</param>
    /// <param name="ct">CancellationToken</param>
    /// <returns></returns>
    public Task DeleteUserAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Получение информации о доступных подписках.
    /// Требует разрешение <c>readSubscriptionPlans</c>.
    /// </summary>
    /// <param name="ct">SubscriptionPlan</param>
    /// <returns></returns>
    public Task<IEnumerable<SubscriptionPlan>> GetSubscriptionPlansAsync(CancellationToken ct = default);

    /// <summary>
    /// Получение системных данных о подписках. Эти данные указываются в интерфейсе администратора в формате json
    /// и могут быть использованы, например, для хранения лимитов.
    /// Требует разрешение <c>readSubscriptionPlans</c>.
    /// </summary>
    /// <param name="ct">CancellationToken</param>
    /// <typeparam name="T">Тип, соответствующий используемой json-схеме</typeparam>
    /// <returns></returns>
    public Task<Dictionary<string, T>> GetSubscriptionPlansDataAsync<T>(CancellationToken ct = default);
}