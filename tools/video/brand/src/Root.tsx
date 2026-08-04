import {Composition} from 'remotion';
import {BrandBumper} from './BrandBumper';

export const Root: React.FC = () => {
  return (
    <Composition
      id="BrandBumper"
      component={BrandBumper}
      durationInFrames={280}
      fps={30}
      width={1280}
      height={720}
    />
  );
};
