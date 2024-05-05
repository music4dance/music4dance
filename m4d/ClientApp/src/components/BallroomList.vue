<script setup lang="ts">
defineProps<{ name?: string }>();
</script>

<template>
  <div>
    <p>
      <slot>
        {{ name }} is a category of
        <a href="/dances/ballroom-competition-categories" target="_blank"
          >competition ballroom dancing</a
        >
        that is defined by the
        <a href="http://www.worlddancesport.org/Rule/Athlete/Competition" target="_blank"
          >World Dance Council</a
        >
        and
        <a href="https://www.ndca.org/pages/ndca_rule_book/Default.asp" target="_blank"
          >National Dance Council of America</a
        >.
      </slot>
    </p>
    <ul style="list-style-type: disc">
      <li>
        Click on any of the tempos in the table below (in the MPM, DanceSport, or NDCA columns) to
        go to a list of songs of that tempo
      </li>
      <li>Click on the dance names to go to our details information page for that dance style</li>
      <li>
        Read our
        <a
          href="https://music4dance.blog/im-a-competition-ballroom-dancer-can-i-find-practice-songs-that-are-a-specific-tempo/"
          target="_blank"
        >
          blog post</a
        >
        for step by step instructions on using our catalog to find music of specific style and
        tempo.
      </li>
      <li>
        <a href="/identity/account/register">Register</a> (it's free) to start building your own
        music lists.
      </li>
    </ul>
  </div>
</template>
