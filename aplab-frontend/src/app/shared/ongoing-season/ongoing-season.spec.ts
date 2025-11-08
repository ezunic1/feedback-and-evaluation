import { ComponentFixture, TestBed } from '@angular/core/testing';

import { OngoingSeason } from './ongoing-season';

describe('OngoingSeason', () => {
  let component: OngoingSeason;
  let fixture: ComponentFixture<OngoingSeason>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [OngoingSeason]
    })
    .compileComponents();

    fixture = TestBed.createComponent(OngoingSeason);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
