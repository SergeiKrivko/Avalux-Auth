<script lang="ts">
  import LoginView from './lib/LoginView.svelte'
  import ProfileView from './lib/ProfileView.svelte'
  import {getView} from './lib/routing'

  const view = getView()
</script>

{#if view === 'profile'}
  <ProfileView />
{:else}
  <LoginView />
{/if}
