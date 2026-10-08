import { CommonModule, isPlatformBrowser } from '@angular/common';
import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  EventEmitter,
  Inject,
  Input,
  NgZone,
  OnChanges,
  OnDestroy,
  Output,
  PLATFORM_ID,
  SimpleChanges,
  ViewChild,
} from '@angular/core';

interface ScratchPoint {
  x: number;
  y: number;
  radius: number;
}

@Component({
  selector: 'app-scratch-layer',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './scratch-layer.component.html',
  styleUrl: './scratch-layer.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ScratchLayerComponent implements AfterViewInit, OnChanges, OnDestroy {
  private static readonly revealThreshold = 0.19;
  private static readonly focusRevealThreshold = 0.28;
  private static readonly brushRadius = 20;

  @Input() forceReveal = false;
  @Input() disabled = false;
  @Input() hideContentUntilRevealed = false;
  @Input() ariaLabel = 'Zona raspable';
  @Output() scratchStarted = new EventEmitter<void>();
  @Output() thresholdReached = new EventEmitter<void>();
  @ViewChild('scratchCanvas') private canvasRef?: ElementRef<HTMLCanvasElement>;

  completed = false;
  private browserReady = false;
  private drawing = false;
  private startEmitted = false;
  private moveCounter = 0;
  private resizeObserver?: ResizeObserver;
  private readonly points: ScratchPoint[] = [];

  constructor(
    @Inject(PLATFORM_ID) private readonly platformId: object,
    private readonly zone: NgZone
  ) {}

  ngAfterViewInit(): void {
    if (!isPlatformBrowser(this.platformId)) return;
    this.browserReady = true;
    this.drawCover();
    this.zone.runOutsideAngular(() => {
      this.resizeObserver = new ResizeObserver(() => this.drawCover());
      const canvas = this.canvasRef?.nativeElement;
      if (canvas) this.resizeObserver.observe(canvas);
    });
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['forceReveal'] && this.forceReveal) {
      this.completed = true;
      this.drawing = false;
    }
  }

  ngOnDestroy(): void {
    this.resizeObserver?.disconnect();
    this.resizeObserver = undefined;
    this.drawing = false;
  }

  onPointerDown(event: PointerEvent): void {
    if (!this.canScratch()) return;
    event.preventDefault();
    const canvas = this.canvasRef?.nativeElement;
    if (!canvas) return;
    canvas.setPointerCapture(event.pointerId);
    this.drawing = true;
    if (!this.startEmitted) {
      this.startEmitted = true;
      this.scratchStarted.emit();
    }
    this.erase(event);
  }

  onPointerMove(event: PointerEvent): void {
    if (!this.drawing || !this.canScratch()) return;
    event.preventDefault();
    this.erase(event);
    this.moveCounter++;
    if (this.moveCounter % 2 === 0) this.evaluateCoverage();
  }

  onPointerEnd(event: PointerEvent): void {
    if (!this.drawing) return;
    event.preventDefault();
    this.drawing = false;
    const canvas = this.canvasRef?.nativeElement;
    if (canvas?.hasPointerCapture(event.pointerId)) {
      canvas.releasePointerCapture(event.pointerId);
    }
    this.evaluateCoverage();
  }

  private canScratch(): boolean {
    return this.browserReady && !this.disabled && !this.forceReveal && !this.completed;
  }

  private drawCover(): void {
    const canvas = this.canvasRef?.nativeElement;
    if (!canvas || this.completed || this.forceReveal) return;
    const rectangle = canvas.getBoundingClientRect();
    if (!rectangle.width || !rectangle.height) return;
    const pixelRatio = Math.max(1, window.devicePixelRatio || 1);
    canvas.width = Math.round(rectangle.width * pixelRatio);
    canvas.height = Math.round(rectangle.height * pixelRatio);
    const context = canvas.getContext('2d', { willReadFrequently: true });
    if (!context) return;

    context.globalCompositeOperation = 'source-over';
    const gradient = context.createLinearGradient(0, 0, canvas.width, canvas.height);
    gradient.addColorStop(0, '#d9dde4');
    gradient.addColorStop(0.5, '#aeb5c0');
    gradient.addColorStop(1, '#e5e8ed');
    context.fillStyle = gradient;
    context.fillRect(0, 0, canvas.width, canvas.height);
    context.fillStyle = 'rgba(255, 255, 255, .22)';
    for (let index = 0; index < 72; index++) {
      const x = (index * 47) % canvas.width;
      const y = (index * 83) % canvas.height;
      context.fillRect(x, y, Math.max(1, pixelRatio), Math.max(1, pixelRatio));
    }

    for (const point of this.points) this.erasePoint(context, canvas, point);
  }

  private erase(event: PointerEvent): void {
    const canvas = this.canvasRef?.nativeElement;
    if (!canvas) return;
    const rectangle = canvas.getBoundingClientRect();
    if (!rectangle.width || !rectangle.height) return;
    const point: ScratchPoint = {
      x: (event.clientX - rectangle.left) / rectangle.width,
      y: (event.clientY - rectangle.top) / rectangle.height,
      radius: ScratchLayerComponent.brushRadius / Math.min(rectangle.width, rectangle.height),
    };
    this.points.push(point);
    const context = canvas.getContext('2d', { willReadFrequently: true });
    if (context) this.erasePoint(context, canvas, point);
  }

  private erasePoint(
    context: CanvasRenderingContext2D,
    canvas: HTMLCanvasElement,
    point: ScratchPoint
  ): void {
    context.save();
    context.globalCompositeOperation = 'destination-out';
    context.beginPath();
    context.arc(
      point.x * canvas.width,
      point.y * canvas.height,
      point.radius * Math.min(canvas.width, canvas.height),
      0,
      Math.PI * 2
    );
    context.fill();
    context.restore();
  }

  private evaluateCoverage(): void {
    const canvas = this.canvasRef?.nativeElement;
    const context = canvas?.getContext('2d', { willReadFrequently: true });
    if (!canvas || !context || this.completed) return;

    const pixels = context.getImageData(0, 0, canvas.width, canvas.height).data;
    const sampleStep = Math.max(1, Math.floor(Math.min(canvas.width, canvas.height) / 40));
    const focusLeft = canvas.width * 0.2;
    const focusRight = canvas.width * 0.8;
    const focusTop = canvas.height * 0.2;
    const focusBottom = canvas.height * 0.8;
    let transparent = 0;
    let sampled = 0;
    let focusTransparent = 0;
    let focusSampled = 0;

    for (let y = 0; y < canvas.height; y += sampleStep) {
      for (let x = 0; x < canvas.width; x += sampleStep) {
        const isTransparent = pixels[(y * canvas.width + x) * 4 + 3] < 32;
        sampled++;
        if (isTransparent) transparent++;

        if (x >= focusLeft && x <= focusRight && y >= focusTop && y <= focusBottom) {
          focusSampled++;
          if (isTransparent) focusTransparent++;
        }
      }
    }

    const totalCoverage = sampled ? transparent / sampled : 0;
    const focusCoverage = focusSampled ? focusTransparent / focusSampled : 0;
    if (
      totalCoverage < ScratchLayerComponent.revealThreshold &&
      focusCoverage < ScratchLayerComponent.focusRevealThreshold
    ) return;

    this.completed = true;
    this.drawing = false;
    this.thresholdReached.emit();
  }
}
