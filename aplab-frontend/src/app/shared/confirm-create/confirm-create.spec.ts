import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ConfirmCreate } from './confirm-create';

describe('ConfirmCreate', () => {
  let component: ConfirmCreate;
  let fixture: ComponentFixture<ConfirmCreate>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ConfirmCreate]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ConfirmCreate);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
