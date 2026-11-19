<script lang="ts">
  import {onMount} from 'svelte'
  import {confirmEmail, friendlyError} from './api'
  import {clearFragmentToken, getFragmentToken, redirectTo} from './routing'

  type Mode = 'checking' | 'confirmed' | 'invalid'

  let mode = $state<Mode>('checking')
  let formError = $state<string | null>(null)

  // R-02: the token is read from the URL fragment, then the address bar is
  // cleaned before the request. A mail scanner that only prefetches the page
  // cannot redeem the link, because the token never reaches the server on GET.
  onMount(() => {
    const token = getFragmentToken()
    clearFragmentToken()

    if (!/^[0-9a-f]{64}$/.test(token)) {
      mode = 'invalid'
      return
    }

    void submit(token)
  })

  async function submit(token: string) {
    try {
      const response = await confirmEmail(token)

      // The link also finishes the sign-in that registration interrupted, so the
      // browser goes straight back to the client application with a fresh code.
      if (response?.redirectUrl) {
        redirectTo(response.redirectUrl)
        return
      }

      // A link sent after an address change only confirms the mailbox; the user
      // is already signed in elsewhere.
      mode = 'confirmed'
    } catch (error) {
      formError = friendlyError(error, 'confirm')
      mode = 'invalid'
    }
  }
</script>

<svelte:head>
  <title>Подтверждение email — Avalux Auth</title>
</svelte:head>

<main class="page">
  <section class="card" aria-labelledby="confirm-title">
    <header class="brand">
      <span class="brand-mark" aria-hidden="true">A</span>
      <span class="brand-text">
        <span class="brand-name">Avalux Auth</span>
        <span class="brand-client">Подтверждение email</span>
      </span>
    </header>

    {#if mode === 'checking'}
      <div class="status" role="status">
        <span class="spinner" aria-hidden="true"></span>
        <span>Подтверждаем email…</span>
      </div>
    {:else if mode === 'confirmed'}
      <div class="heading">
        <h1 id="confirm-title">Email подтверждён</h1>
        <p>Адрес подтверждён — теперь на него можно восстановить пароль. Вернитесь в приложение.</p>
      </div>

      <div class="alert alert--success" role="status">
        Если это были не вы, смените пароль и свяжитесь с администратором сервиса.
      </div>
    {:else}
      <div class="heading">
        <h1 id="confirm-title">Ссылка недействительна</h1>
        <p>
          {formError ??
            'Ссылка устарела или уже была использована. Вернитесь в приложение и войдите как обычно.'}
        </p>
      </div>

      <div class="alert alert--error" role="alert">
        Подтверждение необязательно — приложением можно пользоваться и без него.
      </div>
    {/if}
  </section>
</main>
