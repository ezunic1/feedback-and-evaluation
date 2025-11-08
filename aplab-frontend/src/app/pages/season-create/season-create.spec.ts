import { ComponentFixture, TestBed } from '@angular/core/testing';

import { SeasonCreate } from './season-create';

describe('SeasonCreate', () => {
  let component: SeasonCreate;
  let fixture: ComponentFixture<SeasonCreate>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SeasonCreate]
    })
    .compileComponents();

    fixture = TestBed.createComponent(SeasonCreate);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
