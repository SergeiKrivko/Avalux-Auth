<script lang="ts">
  import {ApiError, uploadAvatar} from './api'

  let {
    value = $bindable<string | null>(null),
    uploading = $bindable(false),
    name = '',
    stateParam = '',
    disabled = false,
  }: {
    value?: string | null
    uploading?: boolean
    name?: string
    stateParam?: string
    disabled?: boolean
  } = $props()

  let input: HTMLInputElement | undefined
  let error = $state<string | null>(null)

  const preview = $derived(value ? `/api/v1/avatar/${value}` : null)
  const initials = $derived((name.trim().charAt(0) || 'A').toUpperCase())

  async function onFile(event: Event) {
    const target = event.currentTarget as HTMLInputElement
    const file = target.files?.[0]
    target.value = ''
    if (!file) return

    error = null
    if (!file.type.startsWith('image/')) {
      error = 'Выберите файл изображения.'
      return
    }
    if (file.size > 10 * 1024 * 1024) {
      error = 'Изображение не должно быть больше 10 МБ.'
      return
    }
    if (!stateParam) {
      error = 'Ссылка недействительна. Вернитесь в приложение и начните вход заново.'
      return
    }

    uploading = true
    try {
      value = await uploadAvatar(file, stateParam)
    } catch (err) {
      if (err instanceof ApiError && err.status === 429) {
        error = 'Слишком много загрузок. Подождите немного и попробуйте ещё раз.'
      } else if (err instanceof ApiError && err.status === 401) {
        error = 'Ссылка недействительна или устарела. Вернитесь в приложение и начните вход заново.'
      } else {
        error = 'Не удалось загрузить изображение. Попробуйте ещё раз.'
      }
    } finally {
      uploading = false
    }
  }
</script>

<div class="avatar">
  <span class="avatar-preview" class:avatar-preview--loading={uploading}>
    {#if preview}
      <img src={preview} alt="Аватар" />
    {:else}
      <span class="avatar-initials" aria-hidden="true">{initials}</span>
    {/if}
    {#if uploading}
      <span class="avatar-spinner spinner" aria-hidden="true"></span>
    {/if}
  </span>
  <div class="avatar-actions">
    <button
      class="button button--ghost button--small"
      type="button"
      onclick={() => input?.click()}
      disabled={disabled || uploading}
    >
      {value ? 'Заменить фото' : 'Загрузить фото'}
    </button>
    <span class="field-hint">PNG, JPG или WebP, до 10 МБ</span>
    {#if error}
      <span class="field-error" role="alert">{error}</span>
    {/if}
  </div>
  <input
    bind:this={input}
    class="visually-hidden"
    type="file"
    accept="image/*"
    onchange={onFile}
    disabled={disabled || uploading}
    tabindex="-1"
  />
</div>
