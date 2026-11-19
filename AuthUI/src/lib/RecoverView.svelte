<script lang="ts">
  import {onMount} from 'svelte'
  import Field from './Field.svelte'
  import {ApiError, completePasswordReset, friendlyError, requestPasswordReset} from './api'
  import {clearResetHash, getResetToken} from './routing'
  import {validateNewPassword, type FieldErrors} from './validation'

  type Mode = 'request' | 'reset' | 'invalid' | 'done'

  let mode = $state<Mode>('request')
  let token = $state('')

  let identifier = $state('')
  let password = $state('')
  let passwordAgain = $state('')

  let errors = $state<FieldErrors>({})
  let formError = $state<string | null>(null)
  let submitting = $state(false)
  let requestSent = $state(false)

  // R-01: the token is read from the URL fragment, then the address bar is
  // cleaned so the one-time secret is not kept in history or copied by accident.
  onMount(() => {
    const value = getResetToken()
    if (!value) return

    clearResetHash()
    if (!/^[0-9a-f]{64}$/.test(value)) {
      mode = 'invalid'
      return
    }

    token = value
    mode = 'reset'
  })

  async function submitRequest(event: SubmitEvent) {
    event.preventDefault()
    if (submitting) return

    formError = null
    const value = identifier.trim()
    if (!value) {
      errors = {login: 'Введите логин или email.'}
      return
    }
    if (value.length > 254) {
      errors = {login: 'Значение слишком длинное.'}
      return
    }

    errors = {}
    submitting = true
    try {
      await requestPasswordReset(value)
      requestSent = true
    } catch (error) {
      formError = friendlyError(error, 'recover')
    } finally {
      submitting = false
    }
  }

  async function submitReset(event: SubmitEvent) {
    event.preventDefault()
    if (submitting) return

    formError = null
    const next: FieldErrors = {}

    const passwordError = validateNewPassword(password)
    if (passwordError) next.password = passwordError
    if (!passwordAgain) next.passwordAgain = 'Повторите пароль.'
    else if (passwordAgain !== password) next.passwordAgain = 'Пароли не совпадают.'

    errors = next
    if (Object.keys(next).length > 0) return

    submitting = true
    try {
      await completePasswordReset(token, password)
      mode = 'done'
    } catch (error) {
      if (error instanceof ApiError && error.status === 400) {
        mode = 'invalid'
        return
      }
      formError = friendlyError(error, 'reset')
    } finally {
      submitting = false
    }
  }

  function restart() {
    mode = 'request'
    token = ''
    requestSent = false
    formError = null
    errors = {}
    password = ''
    passwordAgain = ''
  }
</script>

<svelte:head>
  <title>Восстановление пароля — Avalux Auth</title>
</svelte:head>

<main class="page">
  <section class="card" aria-labelledby="recover-title">
    <header class="brand">
      <span class="brand-mark" aria-hidden="true">A</span>
      <span class="brand-text">
        <span class="brand-name">Avalux Auth</span>
        <span class="brand-client">Восстановление доступа</span>
      </span>
    </header>

    {#if mode === 'request'}
      {#if requestSent}
        <div class="heading">
          <h1 id="recover-title">Проверьте почту</h1>
          <p>
            Если аккаунт с такими данными существует и в нём указан email, мы отправили письмо
            со ссылкой. Ссылка действует ограниченное время и может быть использована один раз.
          </p>
        </div>

        <div class="alert alert--success" role="status">
          Письмо может прийти не сразу. Проверьте папку «Спам» и адрес, указанный в профиле.
        </div>

        <button class="button button--ghost" type="button" onclick={restart}>
          Отправить ещё раз
        </button>
      {:else}
        <div class="heading">
          <h1 id="recover-title">Восстановление пароля</h1>
          <p>Укажите логин или email из профиля — мы отправим одноразовую ссылку для смены пароля.</p>
        </div>

        <form onsubmit={submitRequest} novalidate>
          <Field
            label="Логин или email"
            bind:value={identifier}
            autocomplete="username"
            placeholder="Ваш логин или you@example.com"
            error={errors.login}
            hint="Если вы входили только через внешний сервис, восстановление не нужно — войдите тем же способом. Без email в профиле восстановление недоступно."
            disabled={submitting}
          />

          {#if formError}
            <div class="alert alert--error" role="alert">{formError}</div>
          {/if}

          <button class="button" type="submit" disabled={submitting}>
            {#if submitting}
              <span class="spinner spinner--light" aria-hidden="true"></span>
            {/if}
            Отправить ссылку
          </button>
        </form>

        <p class="fineprint">
          <button class="link-button" type="button" onclick={() => history.back()}>
            Вернуться назад
          </button>
        </p>
      {/if}
    {:else if mode === 'reset'}
      <div class="heading">
        <h1 id="recover-title">Новый пароль</h1>
        <p>Придумайте новый пароль. Ссылка одноразовая: после сохранения войти по ней снова не получится.</p>
      </div>

      <form onsubmit={submitReset} novalidate>
        <Field
          label="Новый пароль"
          type="password"
          bind:value={password}
          autocomplete="new-password"
          placeholder="Придумайте пароль"
          error={errors.password}
          hint="Минимум 8 символов"
          disabled={submitting}
        />

        <Field
          label="Пароль ещё раз"
          type="password"
          bind:value={passwordAgain}
          autocomplete="new-password"
          placeholder="Повторите пароль"
          error={errors.passwordAgain}
          disabled={submitting}
        />

        {#if formError}
          <div class="alert alert--error" role="alert">{formError}</div>
        {/if}

        <button class="button" type="submit" disabled={submitting}>
          {#if submitting}
            <span class="spinner spinner--light" aria-hidden="true"></span>
          {/if}
          Сохранить новый пароль
        </button>
      </form>

      <p class="fineprint">
        <button class="link-button" type="button" onclick={restart}>
          Запросить новую ссылку
        </button>
      </p>
    {:else if mode === 'invalid'}
      <div class="heading">
        <h1 id="recover-title">Ссылка недействительна</h1>
        <p>Ссылка устарела или уже была использована. Запросите восстановление пароля заново.</p>
      </div>

      <div class="alert alert--error" role="alert">
        Ссылка действует ограниченное время и срабатывает только один раз.
      </div>

      <button class="button" type="button" onclick={restart}>
        Запросить новую ссылку
      </button>
    {:else}
      <div class="heading">
        <h1 id="recover-title">Пароль изменён</h1>
        <p>Все активные сессии завершены. Вернитесь в приложение и войдите с новым паролем.</p>
      </div>

      <div class="alert alert--success" role="status">
        Если вы не меняли пароль, немедленно свяжитесь с администратором сервиса.
      </div>
    {/if}
  </section>
</main>
