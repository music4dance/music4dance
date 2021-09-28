export interface TourStep {
  target: string;
  content: string;
}

export interface TourOptions {
  labels?: {
    buttonSkip?: string;
    buttonPrevious?: string;
    buttonNext?: string;
    buttonStop?: string;
  };
  enabledButtons?: {
    buttonSkip?: boolean;
    buttonPrevious?: boolean;
    buttonNext?: boolean;
    buttonStop?: boolean;
  };
  startTimeout?: number;
  stopOnTargetNotFound?: boolean;
  useKeyboardNavigation?: boolean;
  enabledNavigationKeys?: {
    escape?: boolean;
    arrowRight?: boolean;
    arrowLeft?: boolean;
  };
  debug?: boolean;
}

export interface TourCallbacks {
  onStart?: () => void;
  onPreviousStep?: (currentStep: number) => void;
  onNextStep?: (currentStep: number) => void;
  onStop?: () => void;
  onSkip?: () => void;
  onFinish?: () => void;
}
