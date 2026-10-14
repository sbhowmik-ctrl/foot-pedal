import styles from "./PedalUnit.module.css";

const PEDALS = [
  { key: "←", sub: "Left" },
  { key: "Space", sub: "Center" },
  { key: "→", sub: "Right" },
] as const;

type PedalUnitProps = {
  /** Show Left / Center / Right under each key label. */
  labeled?: boolean;
};

export default function PedalUnit({ labeled = false }: PedalUnitProps) {
  return (
    <div className={`${styles.unit} ${labeled ? styles.labeled : ""}`}>
      <div className={styles.base} />
      <div className={styles.cable} />
      <div className={styles.pedals}>
        {PEDALS.map((p) => (
          <div key={p.sub} className={styles.pedal}>
            <div className={styles.pad}>
              <span className={styles.label}>{p.key}</span>
              {labeled ? <span className={styles.sublabel}>{p.sub}</span> : null}
              <div className={styles.stem} />
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
