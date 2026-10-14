import PedalUnit from "./PedalUnit";
import styles from "./PedalBackground.module.css";

function PedalUnitScene({ secondary }: { secondary?: boolean }) {
  return (
    <div className={secondary ? `${styles.scene} ${styles.sceneSecondary}` : styles.scene}>
      <div className={styles.rig}>
        <PedalUnit />
      </div>
    </div>
  );
}

/** Decorative 3D foot-pedal scene fixed behind page content. */
export default function PedalBackground() {
  return (
    <div className={styles.wrap} aria-hidden="true">
      <div className={styles.veil} />
      <div className={styles.chips}>
        <span className={styles.chip}>←</span>
        <span className={styles.chip}>Space</span>
        <span className={styles.chip}>→</span>
        <span className={styles.chip}>USB</span>
      </div>
      <PedalUnitScene />
      <PedalUnitScene secondary />
    </div>
  );
}
