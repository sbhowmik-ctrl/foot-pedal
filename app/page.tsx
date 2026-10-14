import PedalHeroIllustration from "./PedalHeroIllustration";
import styles from "./page.module.css";

const DOWNLOAD_URL = "/downloads/PedalKeys-Windows.zip";

const MAPPING = [
  {
    pedal: "Left",
    key: "←",
    keyName: "Left arrow",
    action: "Seek back 1 second",
    hold: "Keeps subtracting 1 second",
  },
  {
    pedal: "Center / Play",
    key: "Space",
    keyName: "Space",
    action: "Play and pause",
    hold: "Once per press",
  },
  {
    pedal: "Right",
    key: "→",
    keyName: "Right arrow",
    action: "Seek forward 1 second",
    hold: "Keeps adding 1 second",
  },
];

const STEPS = [
  { title: "Run setup", detail: "Extract the zip and double-click setup.bat. No administrator prompt." },
  { title: "Leave the tray app running", detail: "PedalKeys sits in the system tray and starts with Windows." },
  { title: "Click the player once", detail: "Bring YouTube, a downloaded video, or an audio file to the front so it is the active window." },
  { title: "Use the pedal", detail: "Tap to skip 1 second, hold to keep skipping 1 second at a time, press the center to play or pause." },
];

function DownloadButton({ className }: { className?: string }) {
  return (
    <a className={`${styles.button} ${className ?? ""}`} href={DOWNLOAD_URL} download>
      <svg aria-hidden="true" viewBox="0 0 24 24" width="20" height="20" fill="currentColor">
        <path d="M3 5.5 10.5 4.4v7.1H3V5.5Zm8.6-1.3L21 2.8v8.7h-9.4V4.2ZM3 12.5h7.5v7.1L3 18.5v-6Zm8.6 0H21v8.7l-9.4-1.4v-7.3Z" />
      </svg>
      Download for Windows
    </a>
  );
}

export default function Home() {
  return (
    <main className={styles.page}>
      <header className={styles.hero}>
        <div className={styles.heroText}>
          <p className={styles.eyebrow}>For Sanguine Preon and VEC Infinity USB foot pedals</p>
          <h1>Your foot pedal, working in YouTube and every other app.</h1>
          <p className={styles.lead}>
            Skip back, play and pause, and skip forward with your feet while your hands stay on the
            keyboard. PedalKeys is already set up: install it and it works. There is no mapping
            screen to open.
          </p>
          <div className={styles.cta}>
            <DownloadButton />
            <p className={styles.ctaNote}>Windows 10 and 11 · free · no account · no admin rights</p>
          </div>
        </div>
        <PedalHeroIllustration />
      </header>

      <section aria-labelledby="mapping-heading" className={styles.section}>
        <h2 id="mapping-heading">What each pedal does</h2>
        <div className={styles.tableWrap}>
          <table className={styles.table}>
            <thead>
              <tr>
                <th scope="col">Pedal</th>
                <th scope="col">Sends</th>
                <th scope="col">Single press</th>
                <th scope="col">While held</th>
              </tr>
            </thead>
            <tbody>
              {MAPPING.map((row) => (
                <tr key={row.pedal}>
                  <th scope="row">{row.pedal}</th>
                  <td>
                    <kbd aria-label={row.keyName}>{row.key}</kbd>
                  </td>
                  <td>{row.action}</td>
                  <td>{row.hold}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <p className={styles.small}>
          Forward and back seek the playing video or audio by 1 second, including YouTube and
          files you open in Chrome, Edge, Windows Media Player or Groove. Play/pause still sends
          Space to whichever window is focused.
        </p>
      </section>

      <section aria-labelledby="after-heading" className={styles.section}>
        <h2 id="after-heading">After you install</h2>
        <ol className={styles.steps}>
          {STEPS.map((s) => (
            <li key={s.title}>
              <strong>{s.title}</strong>
              <span>{s.detail}</span>
            </li>
          ))}
        </ol>
        <DownloadButton className={styles.buttonSecondary} />
      </section>
    </main>
  );
}
