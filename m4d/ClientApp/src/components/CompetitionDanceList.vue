<script setup lang="ts">
interface Props {
  name?: string;
  organizationType?: "ballroom" | "country" | "category";
}

const props = withDefaults(defineProps<Props>(), {
  name: undefined,
  organizationType: "ballroom",
});

const orgLinks = {
  ballroom: {
    primary: {
      name: "World Dance Council",
      url: "http://www.worlddancesport.org/Rule/Athlete/Competition",
    },
    secondary: {
      name: "National Dance Council of America",
      url: "https://www.ndca.org/pages/ndca_rule_book/Default.asp",
    },
  },
  country: {
    primary: {
      name: "United Country Western Dance Council",
      url: "https://ucwdc.org/wp-content/uploads/2023/01/UCWDC-ProAm-ProPro-and-Couples-Competition-Music-BPMs-2023-2025.pdf",
    },
    secondary: {
      name: "World Country Dance Federation",
      url: "https://www1.worldcdf.com/pdf/RULES_BOOK.pdf",
    },
  },
};

const currentOrgs = props.organizationType === "country" ? orgLinks.country : orgLinks.ballroom;
const dancingType = props.organizationType === "country" ? "country western" : "ballroom";
const competitionPageLink =
  props.organizationType === "country"
    ? "/dances/country"
    : "/dances/ballroom-competition-categories";
</script>

<template>
  <div>
    <p>
      <slot>
        <template v-if="name">
          {{ name }} is a category of
          <a :href="competitionPageLink" target="_blank">competition {{ dancingType }} dancing</a>
          that is defined by the
          <a :href="currentOrgs.primary.url" target="_blank">{{ currentOrgs.primary.name }}</a>
          and
          <a :href="currentOrgs.secondary.url" target="_blank">{{ currentOrgs.secondary.name }}</a
          >.
        </template>
        <template v-else>
          Competition {{ dancingType }} dancing is defined by the
          <a :href="currentOrgs.primary.url" target="_blank">{{ currentOrgs.primary.name }}</a>
          and
          <a :href="currentOrgs.secondary.url" target="_blank">{{ currentOrgs.secondary.name }}</a
          >.
        </template>
      </slot>
    </p>
    <ul style="list-style-type: disc">
      <li>
        Click on any of the tempos in the table below (in the MPM,
        <template v-if="organizationType === 'ballroom'">DanceSport, or NDCA</template>
        <template v-else>UCWDC, WORLDCDF, or ACDA</template>
        columns) to go to a list of songs of that tempo
      </li>
      <li>Click on the dance names to go to our details information page for that dance style</li>
      <li>
        Read our
        <a
          href="https://music4dance.blog/im-a-ballroom-dancer-can-i-find-practice-songs-that-are-at-competition-tempo-revisited/"
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
