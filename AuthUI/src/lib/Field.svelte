<script lang="ts">
  import type {FullAutoFill} from 'svelte/elements'

  let {
    label,
    value = $bindable(''),
    type = 'text',
    name = undefined,
    autocomplete = undefined,
    placeholder = '',
    error = null,
    hint = null,
    disabled = false,
    inputmode = undefined,
  }: {
    label: string
    value?: string
    type?: string
    name?: string
    autocomplete?: FullAutoFill
    placeholder?: string
    error?: string | null
    hint?: string | null
    disabled?: boolean
    inputmode?: 'text' | 'email' | 'tel' | 'numeric' | 'decimal' | 'search' | 'url'
  } = $props()

  const id = $props.id()
  const errorId = `${id}-error`
  const hintId = `${id}-hint`

  let visible = $state(false)
  const isPassword = $derived(type === 'password')
  const inputType = $derived(isPassword && visible ? 'text' : type)
  const describedBy = $derived(error ? errorId : hint ? hintId : undefined)
</script>

<div class="field">
  <label class="field-label" for={id}>{label}</label>
  <div
    class="field-control"
    class:field-control--invalid={!!error}
    class:field-control--password={isPassword}
  >
    <input
      {id}
      class="field-input"
      {name}
      {placeholder}
      {disabled}
      {autocomplete}
      {inputmode}
      type={inputType}
      bind:value
      aria-invalid={error ? 'true' : undefined}
      aria-describedby={describedBy}
    />
    {#if isPassword}
      <button
        type="button"
        class="field-toggle"
        aria-label={visible ? 'Скрыть пароль' : 'Показать пароль'}
        aria-pressed={visible}
        onclick={() => (visible = !visible)}
        disabled={disabled}
      >
        {#if visible}
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" aria-hidden="true">
            <path
              d="M3 3l18 18M10.6 10.7a2 2 0 0 0 2.7 2.7M9.9 5.2A10.8 10.8 0 0 1 12 5c5 0 9 4.5 9 7 0 .9-.4 1.9-1.1 2.9M6.2 6.3C4.2 7.6 3 9.5 3 12c0 2.5 4 7 9 7 1.2 0 2.3-.2 3.3-.7"
              stroke="currentColor"
              stroke-width="1.7"
              stroke-linecap="round"
              stroke-linejoin="round"
            />
          </svg>
        {:else}
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" aria-hidden="true">
            <path
              d="M3 12c0-2.5 4-7 9-7s9 4.5 9 7-4 7-9 7-9-4.5-9-7Z"
              stroke="currentColor"
              stroke-width="1.7"
            />
            <circle cx="12" cy="12" r="2.7" stroke="currentColor" stroke-width="1.7" />
          </svg>
        {/if}
      </button>
    {/if}
  </div>
  {#if error}
    <span class="field-error" id={errorId} role="alert">{error}</span>
  {:else if hint}
    <span class="field-hint" id={hintId}>{hint}</span>
  {/if}
</div>
