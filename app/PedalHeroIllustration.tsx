import styles from "./PedalHeroIllustration.module.css";

const PEDALS = [
  { key: "←", name: "Left", hint: "Back 1 second", compactKey: false },
  { key: "Space", name: "Center", hint: "Play / pause", compactKey: true },
  { key: "→", name: "Right", hint: "Forward 1 second", compactKey: false },
] as const;

export default function PedalHeroIllustration() {
  return (
    <div
      className={styles.wrap}
      role="img"
      aria-label="A three-pedal USB foot switch: left pedal sends the left arrow, center sends space, right sends the right arrow."
    >
      <div className={styles.chassis}>
        <div className={styles.cable} aria-hidden="true" />
        <div className={styles.pedals}>
          {PEDALS.map((p) => (
            <div key={p.name} className={styles.pedal}>
              <div className={styles.pad}>
                <span className={p.compactKey ? styles.keyCompact : styles.key}>{p.key}</span>
                <span className={styles.name}>{p.name}</span>
              </div>
              <span className={styles.hint}>{p.hint}</span>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
