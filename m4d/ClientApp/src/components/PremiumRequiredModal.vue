<script setup lang="ts">
defineProps<{
  featureName: string;
}>();

const modelValue = defineModel<boolean>({ default: false });
</script>

<template>
  <BModal v-model="modelValue" title="Premium Feature" ok-only ok-title="Close">
    <h5>{{ featureName }} is a Premium Feature</h5>
    <p>
      This feature is available to premium subscribers. Premium membership provides access to
      exclusive features like adding songs to Spotify playlists, creating custom playlists, and
      more.
    </p>

    <h6>Benefits of Premium Membership:</h6>
    <ul>
      <li>Add songs directly to your Spotify playlists</li>
      <li>Create custom playlists with up to 1000 songs</li>
      <li>Advanced search and filtering capabilities</li>
      <li>Priority support</li>
      <li>Support the continued development of music4dance.net</li>
    </ul>

    <p class="mt-3">
      <strong
        >Ready to upgrade?
        <a href="/home/contribute">Learn more about premium membership</a></strong
      >
    </p>

    <p class="text-muted">
      For more information about this feature, visit our
      <a
        href="https://music4dance.blog/music4dance-help/spotify-playlist/"
        target="_blank"
        rel="noopener"
        >help documentation</a
      >.
    </p>
  </BModal>
</template>
