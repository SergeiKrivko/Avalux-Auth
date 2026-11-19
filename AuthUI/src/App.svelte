<script lang="ts">
  import LoginView from './lib/LoginView.svelte'
  import ProfileView from './lib/ProfileView.svelte'
  import RecoverView from './lib/RecoverView.svelte'
  import {getView} from './lib/routing'

  const view = getView()
</script>

{#if view === 'recover'}
  <RecoverView />
{:else if view === 'profile'}
  <ProfileView />
{:else}
  <LoginView />
{/if}
