import { ComponentFixture, TestBed } from '@angular/core/testing';

import { Season } from './season';

describe('Season', () => {
  let component: Season;
  let fixture: ComponentFixture<Season>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Season]
    })
    .compileComponents();

    fixture = TestBed.createComponent(Season);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
