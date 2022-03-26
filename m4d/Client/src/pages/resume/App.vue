<template>
  <page id="app" title="David's Resume" :breadcrumbs="breadcrumbs">
    <p>
      <a href="/Content/DavidGray-Resume.pdf">Printable Version</a>
    </p>
    <div class="title">David W. Gray</div>
    <div class="section">Skills Profile</div>
    <ul>
      <li>
        C#/.Net, C/C++, JavaScript, TypeScript, Vue.js, MySql, T-SQL, Azure
        Cognitive Search
      </li>
      <li>Full Stack Web, Desktop Application, Diagnostic Tools Development</li>
      <li>
        Leadership experience using both Agile and Waterfall methodologies
      </li>
      <li>Mentor and coach</li>
    </ul>
    <div class="section">Employment History</div>
    <div class="job">
      <div class="job-row">
        <div class="job-title">
          Software Engineering Manager, Clean Power Research
        </div>
        <div class="dates">2017 — Present</div>
      </div>
      <div class="location">Kirkland, WA</div>
      <div class="description">
        <ul>
          <li>
            Managed a team of engineers who develop a family of products
            including consumer facing solar, storage and electric vehicle
            calculators, a B2B API for utility bill calculations, and a utility
            scale ML based prediction engine for distributed energy resource
            adoption. These are SaaS products that are developed using Microsoft
            technologies with a Vue.js/TypeScript front end running in the AWS
            cloud.
          </li>
          <li>
            Rebuilt a team and a code base that had been oriented towards small
            contract-based projects by introducing engineering practices that
            moved towards a more stable and manageable team and system. This
            included introducing SCRUM practices, design and code review
            processes and several levels of automated testing. Average feature
            completion time went from being measured in months to weeks.
          </li>
          <li>
            Implemented SOC2 controls, including passing all four annual audits
            without exceptions. Collaborated with external penetration testers
            to secure the products.
          </li>
          <li>
            Established a quarterly “Management Book Club” and built
            communications between managers to establish management practices
            and train new managers.
          </li>
        </ul>
      </div>
    </div>
    <div class="job">
      <div class="job-row">
        <div class="job-title">Founder, music4dance.net</div>
        <div class="dates">2014 — Present</div>
      </div>
      <div class="location">Seattle, WA</div>
      <div class="description">
        Built a ratings and referral website that helps dancers find music that
        inspires them to dance. This is a Web application written in asp.net
        core with a Vue.js front end, running in the Azure cloud and built
        around Azure Cognitive search.
      </div>
    </div>
    <div class="job">
      <div class="job-row">
        <div class="job-title">Principal Lead Software Engineer, Microsoft</div>
        <div class="dates">2003 — 2012</div>
      </div>
      <div class="location">Redmond, WA</div>
      <div class="description">
        <ul>
          <li>
            Engineering lead for the Visual Studio Debugger, Profiler, and
            IntelliTrace. Drove best of class diagnostics tools through multiple
            product cycles. Attacked many of the hardest problems developers
            face including reducing the “not reproducible” problem and
            diagnosing issues in the cloud, desktop and devices.
          </li>
          <li>
            Collaborated with cross-functional teams across Microsoft including
            Visual Studio, C#, VB, C++, .net, SharePoint and Windows to enable
            features that were business critical to those teams and to enable
            the tools that my team owned to work effectively with those
            platforms and languages.
          </li>
          <li>
            Worked with management and the team to transition to a SCRUM process
          </li>
        </ul>
      </div>
    </div>
    <div class="job">
      <div class="job-row">
        <div class="job-title">President, theGray.com</div>
        <div class="dates">1999 — 2003</div>
      </div>
      <div class="location">Seattle, WA</div>
      <div class="description">
        General software consulting including technical feasibility analysis for
        venture capitalists, website design and deployment, custom office
        automation and billing software for a clinic, web-based survey, and data
        cleaning.
      </div>
    </div>
    <div class="job">
      <div class="job-row">
        <div class="job-title">Vice President of Technology, Sunhawk.com</div>
        <div class="dates">1996 — 1998</div>
      </div>
      <div class="location">Seattle, WA</div>
      <div class="description">
        Managed a cross-functional team of Software Engineers, Graphics Artists
        and Music Librarians to build an e-commerce solution for browsing and
        purchasing sheet music over the internet. This included a web-site
        engine that both compiled static pages served by a Linux server and an
        IIS plugin to dynamically serve pages based on a custom music database
        as well as a system to download encrypted sheet music and purchase the
        encryption key by processing credit card transactions.
      </div>
    </div>
    <div class="job">
      <div class="job-row">
        <div class="job-title">Lead Software Engineer, Microsoft</div>
        <div class="dates">1995 — 1996</div>
      </div>
      <div class="location">Redmond, WA</div>
      <div class="description">
        Led a team that translated some of the most compelling ideas from Visual
        Basic Enterprise Edition including database management and component
        wizards into the first Enterprise Edition of Visual C++.
      </div>
    </div>
    <div class="job">
      <div class="job-row">
        <div class="job-title">Senior Software Engineer, Microsoft</div>
        <div class="dates">1989 — 1995</div>
      </div>
      <div class="location">Redmond, WA</div>
      <div class="description">
        Designed and implemented the core debugger to enable it to target
        multiple operating systems running different microprocessors, locally
        and remotely while hosted in different development environments.
      </div>
    </div>
    <div class="section">Education</div>
    <ul>
      <li>
        B.S. Applied Mathematics and Computer Science, Carnegie Mellon
        University, Pittsburgh
      </li>
      <li>User Experience and Design Certificate, General Assembly, Seattle</li>
    </ul>
    <div class="section">Teaching/Volunteering</div>
    <ul>
      <li>
        Instructor, Discrete Math and Data Structures, Northeastern University,
        Seattle
      </li>
      <li>
        Volunteer Teacher, Introduction to Computer Science, Truman High School,
        Federal Way
      </li>
      <li>
        Instructor, Introduction to Programming Workshop, General Assembly,
        Seattle, Washington
      </li>
      <li>
        Mentor, Computer science students in non-traditional colleges, MinT
      </li>
    </ul>
  </page>
</template>

<script lang="ts">
import Page from "@/components/Page.vue";
import { BreadCrumbItem, infoTrail } from "@/model/BreadCrumbItem";
import { Component, Vue } from "vue-property-decorator";

@Component({
  components: {
    Page,
  },
})
export default class App extends Vue {
  private breadcrumbs: BreadCrumbItem[] = [
    ...infoTrail,
    { text: "About", href: "/home/about" },
    { text: "Resmue", active: true },
  ];
}
</script>

<style lang="scss" scoped>
.title {
  font-size: 3rem;
  font-weight: bold;
}
.section {
  font-size: 2rem;
  font-weight: lighter;
}
.job-title {
  font-weight: bold;
  font-size: 1.25em;
}
.job-row {
  display: flex;
  justify-content: space-between;
}
.location {
  font-weight: lighter;
}
.dates {
  font-weight: lighter;
}
.job {
  margin-bottom: 0.5em;
}
.description {
  font-size: 1.25em;
}
li {
  list-style-type: square;
  font-size: 1.25em;
}
p {
  font-size: 1.25em;
}
</style>
