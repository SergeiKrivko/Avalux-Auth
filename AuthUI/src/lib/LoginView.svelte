<script lang="ts">
  import {onMount} from 'svelte'
  import AvatarInput from './AvatarInput.svelte'
  import Field from './Field.svelte'
  import {friendlyError, getClientInfo, signIn, signUp} from './api'
  import {getState, redirectTo} from './routing'
  import {validateEmail, type FieldErrors} from './validation'

  type Mode = 'signin' | 'signup'

  const stateParam = getState()

  let mode = $state<Mode>('signin')
  let clientName = $state<string | null>(null)
  let loadingClient = $state(true)
  let clientError = $state<string | null>(null)

  let login = $state('')
  let password = $state('')
  let passwordAgain = $state('')
  let username = $state('')
  let email = $state('')
  let avatarId = $state<string | null>(null)
  let avatarUploading = $state(false)

  let errors = $state<FieldErrors>({})
  let formError = $state<string | null>(null)
  let submitting = $state(false)

  const pageTitle = $derived(
    `${mode === 'signin' ? 'Вход' : 'Регистрация'} — ${clientName ?? 'Avalux Auth'}`,
  )

  onMount(() => {
    void loadClient()
  })

  async function loadClient() {
    loadingClient = true
    clientError = null

    if (!stateParam) {
      clientError = 'В ссылке отсутствует параметр state. Откройте форму входа из приложения.'
      loadingClient = false
      return
    }

    try {
      const info = await getClientInfo(stateParam)
      clientName = info?.name?.trim() || null
    } catch (error) {
      clientError = friendlyError(error, 'state')
    } finally {
      loadingClient = false
    }
  }

  function switchMode(next: Mode) {
    if (mode === next) return
    mode = next
    password = ''
    passwordAgain = ''
    errors = {}
    formError = null
  }

  function validate(): boolean {
    const next: FieldErrors = {}

    if (!login.trim()) next.login = 'Введите логин.'
    if (!password) next.password = 'Введите пароль.'
    else if (mode === 'signup' && password.length < 8) {
      next.password = 'Пароль должен содержать минимум 8 символов.'
    }

    if (mode === 'signup') {
      if (!passwordAgain) next.passwordAgain = 'Повторите пароль.'
      else if (passwordAgain !== password) next.passwordAgain = 'Пароли не совпадают.'

      const emailError = validateEmail(email)
      if (emailError) next.email = emailError
    }

    errors = next
    return Object.keys(next).length === 0
  }

  async function submit(event: SubmitEvent) {
    event.preventDefault()
    if (submitting || clientError || avatarUploading) return

    formError = null
    if (!validate()) return

    submitting = true
    try {
      const response =
        mode === 'signin'
          ? await signIn(stateParam, login.trim(), password)
          : await signUp(stateParam, login.trim(), password, {
              name: username.trim() || null,
              email: email.trim() || null,
              avatarId,
            })

      if (!response?.redirectUrl) {
        formError = 'Сервер не вернул адрес возврата. Попробуйте ещё раз.'
        return
      }

      redirectTo(response.redirectUrl)
    } catch (error) {
      formError = friendlyError(error, mode === 'signin' ? 'signin' : 'signup')
    } finally {
      submitting = false
    }
  }
</script>

<svelte:head>
  <title>{pageTitle}</title>
</svelte:head>

<main class="page">
  <section class="card" aria-labelledby="auth-title">
    <header class="brand">
      <span class="brand-mark" aria-hidden="true">A</span>
      <span class="brand-text">
        <span class="brand-name">Avalux Auth</span>
        <span class="brand-client">{clientName ?? 'Безопасная авторизация'}</span>
      </span>
    </header>

    {#if loadingClient}
      <div class="status" role="status">
        <span class="spinner" aria-hidden="true"></span>
        <span>Подключаемся к приложению…</span>
      </div>
    {:else}
      {#if clientError}
        <div class="alert alert--error alert--spaced" role="alert">
          <span>{clientError}</span>
          <button class="link-button" type="button" onclick={() => void loadClient()}>
            Повторить
          </button>
        </div>
      {/if}

      <div class="tabs" role="tablist" aria-label="Режим авторизации">
        <button
          type="button"
          role="tab"
          aria-selected={mode === 'signin'}
          class:active={mode === 'signin'}
          onclick={() => switchMode('signin')}
          disabled={submitting}
        >
          Вход
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={mode === 'signup'}
          class:active={mode === 'signup'}
          onclick={() => switchMode('signup')}
          disabled={submitting}
        >
          Регистрация
        </button>
      </div>

      <div class="heading">
        <h1 id="auth-title">{mode === 'signin' ? 'С возвращением' : 'Создайте аккаунт'}</h1>
        <p>
          {mode === 'signin'
            ? 'Войдите, чтобы продолжить в приложении.'
            : 'Понадобится только логин и пароль.'}
        </p>
      </div>

      <form onsubmit={submit} novalidate>
        <Field
          label="Логин"
          bind:value={login}
          autocomplete="username"
          placeholder="Ваш логин"
          error={errors.login}
          disabled={submitting}
        />

        <Field
          label="Пароль"
          type="password"
          bind:value={password}
          autocomplete={mode === 'signin' ? 'current-password' : 'new-password'}
          placeholder={mode === 'signin' ? 'Ваш пароль' : 'Придумайте пароль'}
          error={errors.password}
          hint={mode === 'signup' ? 'Минимум 8 символов' : null}
          disabled={submitting}
        />

        {#if mode === 'signup'}
          <Field
            label="Пароль ещё раз"
            type="password"
            bind:value={passwordAgain}
            autocomplete="new-password"
            placeholder="Повторите пароль"
            error={errors.passwordAgain}
            disabled={submitting}
          />

          <div class="divider"><span>Профиль</span></div>

          <Field
            label="Имя"
            bind:value={username}
            autocomplete="name"
            placeholder="Необязательно"
            disabled={submitting}
          />

          <Field
            label="Email"
            type="email"
            bind:value={email}
            autocomplete="email"
            inputmode="email"
            placeholder="Необязательно"
            error={errors.email}
            disabled={submitting}
          />

          <div class="avatar-field">
            <span class="field-label">Аватар (необязательно)</span>
            <AvatarInput
              bind:value={avatarId}
              bind:uploading={avatarUploading}
              name={username || login}
              disabled={submitting}
            />
          </div>
        {/if}

        {#if formError}
          <div class="alert alert--error" role="alert">{formError}</div>
        {/if}

        <button class="button" type="submit" disabled={submitting || !!clientError || avatarUploading}>
          {#if submitting}
            <span class="spinner spinner--light" aria-hidden="true"></span>
          {/if}
          {mode === 'signin' ? 'Войти' : 'Создать аккаунт'}
        </button>
      </form>

      <p class="fineprint">Avalux Auth · безопасный вход без передачи пароля приложению</p>
    {/if}
  </section>
</main>
