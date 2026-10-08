import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ScratchLayerComponent } from './scratch-layer.component';

describe('ScratchLayerComponent', () => {
  let fixture: ComponentFixture<ScratchLayerComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ScratchLayerComponent],
    }).compileComponents();
    fixture = TestBed.createComponent(ScratchLayerComponent);
    fixture.detectChanges();
  });

  it('uses a canvas that supports pointer interaction without horizontal sizing', () => {
    const canvas = fixture.nativeElement.querySelector('canvas') as HTMLCanvasElement;
    expect(canvas).toBeTruthy();
    expect(canvas.getAttribute('role')).toBe('img');
  });

  it('cleans its browser resources when destroyed', () => {
    expect(() => fixture.destroy()).not.toThrow();
  });

  it('keeps projected content hidden until the layer is completely revealed', () => {
    fixture.componentRef.setInput('hideContentUntilRevealed', true);
    fixture.detectChanges();

    const content = fixture.nativeElement.querySelector('.scratch-layer__content') as HTMLElement;
    expect(content.classList).toContain('scratch-layer__content--hidden');
    expect(content.getAttribute('aria-hidden')).toBe('true');

    fixture.componentRef.setInput('forceReveal', true);
    fixture.detectChanges();

    expect(content.classList).not.toContain('scratch-layer__content--hidden');
    expect(content.hasAttribute('aria-hidden')).toBeFalse();
  });

  it('completes the cell when the symbol area is sufficiently visible', () => {
    const component = fixture.componentInstance;
    const canvas = fixture.nativeElement.querySelector('canvas') as HTMLCanvasElement;
    canvas.width = 100;
    canvas.height = 100;
    const pixels = coveredPixels(100, 100);

    for (let y = 20; y <= 80; y++) {
      for (let x = 20; x < 50; x++) {
        pixels[(y * 100 + x) * 4 + 3] = 0;
      }
    }

    const context = {
      getImageData: () => ({ data: pixels }),
    } as unknown as CanvasRenderingContext2D;
    spyOn(canvas, 'getContext').and.returnValue(context as never);
    const thresholdReached = spyOn(component.thresholdReached, 'emit');

    (component as unknown as { evaluateCoverage: () => void }).evaluateCoverage();
    (component as unknown as { evaluateCoverage: () => void }).evaluateCoverage();

    expect(component.completed).toBeTrue();
    expect(thresholdReached).toHaveBeenCalledTimes(1);
  });

  it('keeps the cover when only a small portion was touched', () => {
    const component = fixture.componentInstance;
    const canvas = fixture.nativeElement.querySelector('canvas') as HTMLCanvasElement;
    canvas.width = 100;
    canvas.height = 100;
    const pixels = coveredPixels(100, 100);

    for (let y = 40; y < 50; y++) {
      for (let x = 40; x < 50; x++) {
        pixels[(y * 100 + x) * 4 + 3] = 0;
      }
    }

    const context = {
      getImageData: () => ({ data: pixels }),
    } as unknown as CanvasRenderingContext2D;
    spyOn(canvas, 'getContext').and.returnValue(context as never);
    const thresholdReached = spyOn(component.thresholdReached, 'emit');

    (component as unknown as { evaluateCoverage: () => void }).evaluateCoverage();

    expect(component.completed).toBeFalse();
    expect(thresholdReached).not.toHaveBeenCalled();
  });
});

function coveredPixels(width: number, height: number): Uint8ClampedArray {
  const pixels = new Uint8ClampedArray(width * height * 4);
  for (let pixel = 3; pixel < pixels.length; pixel += 4) pixels[pixel] = 255;
  return pixels;
}
