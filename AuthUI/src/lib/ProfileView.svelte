<script lang="ts">
  import {onMount} from 'svelte'
  import AvatarInput from './AvatarInput.svelte'
  import Field from './Field.svelte'
  import {friendlyError, getClientInfo, getProfile, updateProfile} from './api'
  import {getState, redirectTo} from './routing'
  import {validateEmail, type FieldErrors} from './validation'

  const stateParam = getState()

  let loading = $state(true)
  let fatal = $state<string | null>(null)
  let clientName = $state<string | null>(null)

  let username = $state('')
  let email = $state('')
  let avatarId = $state<string | null>(null)
  let avatarUploading = $state(false)

  let oldPassword = $state('')
  let newPassword = $state('')
  let newPasswordAgain = $state('')

  let errors = $state<FieldErrors>({})
  let formError = $state<string | null>(null)
  let submitting = $state(false)

  onMount(() => {
    void load()
  })

  async function load() {
    loading = true
    fatal = null

    if (!stateParam) {
      fatal = 'В ссылке отсутствует параметр state. Откройте форму из приложения.'
      loading = false
      return
    }

    try {
      const [info, profile] = await Promise.all([getClientInfo(stateParam), getProfile(stateParam)])
      if (!profile) {
        fatal = 'Профиль не найден. Вернитесь в приложение и начните вход заново.'
        return
      }

      clientName = info?.name?.trim() || null
      username = profile.name ?? ''
      email = profile.email ?? ''
      avatarId = profile.avatarId ?? null
    } catch (error) {
      fatal = friendlyError(error, 'state')
    } finally {
      loading = false
    }
  }

  function validatePasswordChange(): boolean {
    const next: FieldErrors = {}

    if (!oldPassword) next.oldPassword = 'Введите текущий пароль.'
    if (!newPassword) next.newPassword = 'Введите новый пароль.'
    else if (newPassword.length < 8) {
      next.newPassword = 'Пароль должен содержать минимум 8 символов.'
    }
    if (!newPasswordAgain) next.newPasswordAgain = 'Повторите новый пароль.'
    else if (newPasswordAgain !== newPassword) next.newPasswordAgain = 'Пароли не совпадают.'

    errors = next
    return Object.keys(next).length === 0
  }

  async function submit(event: SubmitEvent) {
    event.preventDefault()
    if (submitting || fatal || avatarUploading) return

    formError = null
    errors = {}
    const wantsPasswordChange = !!(oldPassword || newPassword || newPasswordAgain)

    if (wantsPasswordChange && !validatePasswordChange()) return

    const emailError = validateEmail(email)
    if (emailError) {
      errors = {...errors, email: emailError}
      return
    }
    errors = {}

    submitting = true
    try {
      const response = await updateProfile(stateParam, {
        userInfo: {
          name: username.trim() || null,
          email: email.trim() || null,
          avatarId,
        },
        ...(wantsPasswordChange ? {oldPassword, newPassword} : {}),
      })

      if (!response?.redirectUrl) {
        formError = 'Сервер не вернул адрес возврата. Попробуйте ещё раз.'
        return
      }

      redirectTo(response.redirectUrl)
    } catch (error) {
      formError = friendlyError(error, 'profile')
    } finally {
      submitting = false
    }
  }
</script>

<svelte:head>
  <title>Настройки аккаунта — {clientName ?? 'Avalux Auth'}</title>
</svelte:head>

<main class="page">
  <section class="card card--wide" aria-labelledby="profile-title">
    <header class="brand">
      <span class="brand-mark" aria-hidden="true">A</span>
      <span class="brand-text">
        <span class="brand-name">Avalux Auth</span>
        <span class="brand-client">{clientName ?? 'Безопасная авторизация'}</span>
      </span>
    </header>

    {#if loading}
      <div class="status" role="status">
        <span class="spinner" aria-hidden="true"></span>
        <span>Загружаем профиль…</span>
      </div>
    {:else if fatal}
      <div class="alert alert--error" role="alert">
        <span>{fatal}</span>
        {#if stateParam}
          <button class="link-button" type="button" onclick={() => void load()}>
            Повторить
          </button>
        {/if}
      </div>
    {:else}
      <div class="profile-head">
        <h1 id="profile-title">Настройки аккаунта</h1>
        <p>Измените пароль или обновите данные профиля.</p>
      </div>

      <form onsubmit={submit} novalidate>
        <div class="columns">
          <section class="panel" aria-labelledby="password-title">
            <h2 id="password-title">Смена пароля</h2>
            <p class="panel-hint">
              Оставьте поля пустыми, если менять пароль не нужно.
            </p>

            <Field
              label="Текущий пароль"
              type="password"
              bind:value={oldPassword}
              autocomplete="current-password"
              placeholder="Ваш текущий пароль"
              error={errors.oldPassword}
              disabled={submitting}
            />

            <Field
              label="Новый пароль"
              type="password"
              bind:value={newPassword}
              autocomplete="new-password"
              placeholder="Придумайте новый пароль"
              error={errors.newPassword}
              hint="Минимум 8 символов"
              disabled={submitting}
            />

            <Field
              label="Новый пароль ещё раз"
              type="password"
              bind:value={newPasswordAgain}
              autocomplete="new-password"
              placeholder="Повторите новый пароль"
              error={errors.newPasswordAgain}
              disabled={submitting}
            />
          </section>

          <section class="panel" aria-labelledby="profile-fields-title">
            <h2 id="profile-fields-title">Профиль</h2>
            <p class="panel-hint">Эти данные увидит приложение после входа.</p>

            <Field
              label="Имя"
              bind:value={username}
              autocomplete="name"
              placeholder="Как к вам обращаться"
              disabled={submitting}
            />

            <Field
              label="Email"
              type="email"
              bind:value={email}
              autocomplete="email"
              inputmode="email"
              placeholder="you@example.com"
              error={errors.email}
              hint="Нужен для восстановления пароля."
              disabled={submitting}
            />

            <div class="avatar-field">
              <span class="field-label">Аватар</span>
              <AvatarInput
                bind:value={avatarId}
                bind:uploading={avatarUploading}
                name={username}
                stateParam={stateParam}
                disabled={submitting}
              />
            </div>
          </section>
        </div>

        {#if formError}
          <div class="alert alert--error" role="alert">{formError}</div>
        {/if}

        <div class="actions">
          <button class="button" type="submit" disabled={submitting || avatarUploading}>
            {#if submitting}
              <span class="spinner spinner--light" aria-hidden="true"></span>
            {/if}
            Сохранить и продолжить
          </button>
          <span class="actions-note">После сохранения вы вернётесь в приложение.</span>
        </div>
      </form>
    {/if}
  </section>
</main>
